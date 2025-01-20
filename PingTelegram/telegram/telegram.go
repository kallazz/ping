package telegram

import (
	"context"
	"errors"
	"fmt"
	"log"
	"math/rand"
	"os"
	"strconv"
	"time"

	"github.com/celestix/gotgproto"
	"github.com/celestix/gotgproto/dispatcher/handlers"
	"github.com/celestix/gotgproto/dispatcher/handlers/filters"
	"github.com/celestix/gotgproto/ext"
	"github.com/celestix/gotgproto/sessionMaker"
	"github.com/gotd/td/tg"
	ping "github.com/kallazz/ping/pb"
	"google.golang.org/grpc"
	"google.golang.org/grpc/credentials/insecure"
)

type Client struct {
	C *gotgproto.Client
}

func NewClient() (*Client, error) {
	appidstring, ok := os.LookupEnv("APPID")
	if !ok {
		return nil, errors.New("no APPID Env")
	}
	appid, err := strconv.Atoi(appidstring)
	if err != nil {
		return nil, err
	}
	apihash, ok := os.LookupEnv("APIHASH")
	if !ok {
		return nil, errors.New("no APIHASH Env")
	}
	client, err := gotgproto.NewClient(
		appid,
		apihash,
		gotgproto.ClientTypePhone(os.Getenv("PHONE")),
		&gotgproto.ClientOpts{
			InMemory: true,
			Session:  sessionMaker.SimpleSession(),
		},
	)
	if err != nil {
		return nil, errors.New("failed to create the telegram client.")
	}

	clientDispatcher := client.Dispatcher

	clientDispatcher.AddHandler(handlers.NewMessage(filters.Message.Text, sendMessage))
	//fmt.Println(client)

	return &Client{
		C: client,
	}, nil
}

func sendMessage(ctx *ext.Context, update *ext.Update) error {
	recipients := GetRecipients(update)
	user, chat, channel := GetSender(update)
	var senderUsername string
	if user != nil {
		senderUsername = user.Username
	} else if chat != nil {
		senderUsername = chat.Title
	} else if channel != nil {
		senderUsername = channel.Title
	} else {
		fmt.Println("Sender could not be determined")
	}
	r, err := sendMessageToPingGRPCServer(senderUsername, fmt.Sprintf("%v", recipients), update.EffectiveMessage.GetMessage())
	if err != nil {
		return fmt.Errorf("failed to send message: %v", err)
	}
	fmt.Printf("Response from PING server: %v\n", r)
	return nil
}

func sendMessageToPingGRPCServer(author, recipient, message string) (string, error) {
    host := os.Getenv("HOST")
    port := os.Getenv("PORT")
	address := fmt.Sprintf("%s:%s", host, port)
	conn, err := grpc.NewClient(address, grpc.WithTransportCredentials(insecure.NewCredentials()))
	if err != nil {
		return "", fmt.Errorf("failed to connect with server: %v", err)
	}
	defer conn.Close()
	c := ping.NewPingServiceClient(conn)
	ctx, cancel := context.WithTimeout(context.Background(), time.Second)
	defer cancel()
	msgRequest := &ping.MessageRequest{}
	msgRequest.Client = "Telegram"
	msgRequest.Author = author
	msgRequest.Recipient = recipient
	msgRequest.Message = message
	r, err := c.SendMessage(ctx, msgRequest)
	// log.Printf("Response from gRPC server's SayHello function: %s", r.GetMessage())
	return r.GetMessage(), nil
}

// receiveMessagesFromPingGRPCServer connects to your gRPC server, listens for messages,
// and broadcasts them to Telegram using the provided gotgproto.Client.
func ReceiveMessagesFromPingGRPCServer(client *gotgproto.Client) error {
	fmt.Println("In receive")
    host := os.Getenv("HOST")
    port := os.Getenv("PORT")
	address := fmt.Sprintf("%s:%s", host, port)
	conn, err := grpc.NewClient(address, grpc.WithTransportCredentials(insecure.NewCredentials()))
	if err != nil {
		return fmt.Errorf("failed to connect with gRPC server: %v", err)
	}
	defer conn.Close()

	c := ping.NewPingServiceClient(conn)
	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()

	connect_req := &ping.Empty{Client: "TelegramBot"}

	// Start the server-streaming RPC.
	stream, err := c.ReceiveMessages(ctx, connect_req)
	if err != nil {
		return fmt.Errorf("error starting gRPC stream: %v", err)
	}

	// Continuously read messages from the stream.
	for {
		serverMsg, err := stream.Recv()
		if err != nil {
			return fmt.Errorf("error receiving message from gRPC stream: %v", err)
		}

		log.Printf("Received message from PING server: %v\n", serverMsg)

		// Relay that message to Telegram.
		// If msg is already a Telegram message, you can skip this step.
		if serverMsg.GetMessageResponse().GetType() != "Telegram" {
			if err := broadcastMessageToTelegram(client, serverMsg); err != nil {
				log.Printf("failed to broadcast message to Telegram: %v\n", err)
			}
		}
	}
}

// broadcastMessageToTelegram sends the incoming gRPC ServerMessage to a particular
// Telegram chat (or multiple chats, if you adapt it). In this example, we pull the
// chat ID from an environment variable called TELEGRAM_BROADCAST_CHAT_ID.
func broadcastMessageToTelegram(client *gotgproto.Client, msg *ping.ServerMessage) error {
	fmt.Println("in broadcast message to telegram")

	// The text you want to send to Telegram.
	text := fmt.Sprintf("[%s] %s: %s",
		msg.GetMessageResponse().GetType(),
		msg.GetMessageResponse().GetSender(),
		msg.GetMessageResponse().GetContent(),
	)

	// Get the channel ID from environment or config
	chatIDString, ok := os.LookupEnv("TELEGRAM_BROADCAST_CHAT_ID")
	if !ok {
		return fmt.Errorf("environment variable TELEGRAM_BROADCAST_CHAT_ID not set")
	}

	channelID, err := strconv.ParseInt(chatIDString, 10, 64)
	if err != nil {
		return fmt.Errorf("invalid channel ID: %v", err)
	}

	// Fetch the AccessHash for the channel
	accessHash, err := GetChannelAccessHash(client, channelID)
	if err != nil {
		return fmt.Errorf("failed to get access hash: %v", err)
	}

	// Use InputPeerChannel to send the message
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()

	log.Printf("Sending message to channel: %s\n", text)
	_, err = client.API().MessagesSendMessage(ctx, &tg.MessagesSendMessageRequest{
		Peer: &tg.InputPeerChannel{
			ChannelID:  channelID,
			AccessHash: accessHash,
		},
		Message:  text,
		RandomID: rand.Int63(),
	})
	if err != nil {
		return fmt.Errorf("failed to send Telegram message: %v", err)
	}

	return nil
}

func printMessageToConsole(ctx *ext.Context, update *ext.Update) error {
	senderUsername, ok := update.EffectiveUser().GetUsername()
	if !ok {
		return errors.New("Sender's username not set")
	}
	messageText := update.EffectiveMessage.GetMessage()
	fmt.Println(ctx)
	fmt.Println(update)
	fmt.Println(senderUsername, messageText)
	return nil
}

func GetRecipients(u *ext.Update) []string {
	if u.EffectiveMessage == nil {
		return nil
	}

	var recipients []string
	peer := u.EffectiveMessage.PeerID

	switch p := peer.(type) {
	case *tg.PeerUser:
		user, exists := u.Entities.Users[p.UserID]
		if exists {
			recipients = append(recipients, user.Username)
		} else {
			recipients = append(recipients, fmt.Sprintf("UserID:%d", p.UserID))
		}

	case *tg.PeerChat:
		chat, exists := u.Entities.Chats[p.ChatID]
		if exists {
			recipients = append(recipients, chat.Title)
		} else {
			recipients = append(recipients, fmt.Sprintf("ChatID:%d", p.ChatID))
		}

	case *tg.PeerChannel:
		channel, exists := u.Entities.Channels[p.ChannelID]
		if exists {
			recipients = append(recipients, channel.Title)
		} else {
			recipients = append(recipients, fmt.Sprintf("ChannelID:%d", p.ChannelID))
		}
	}

	return recipients
}

func GetSender(u *ext.Update) (*tg.User, *tg.Chat, *tg.Channel) {
	if u.EffectiveMessage == nil || u.Entities == nil {
		return nil, nil, nil
	}

	peer := u.EffectiveMessage.PeerID
	switch p := peer.(type) {
	case *tg.PeerUser:
		// Sender is a user
		user, exists := u.Entities.Users[p.UserID]
		if exists {
			return user, nil, nil
		}
	case *tg.PeerChat:
		// Sender is a group chat
		chat, exists := u.Entities.Chats[p.ChatID]
		if exists {
			return nil, chat, nil
		}
	case *tg.PeerChannel:
		// Sender is a channel or supergroup
		channel, exists := u.Entities.Channels[p.ChannelID]
		if exists {
			return nil, nil, channel
		}
	}
	return nil, nil, nil
}

func GetChannelAccessHash(client *gotgproto.Client, channelID int64) (int64, error) {
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()

	inputChannel := &tg.InputChannel{
		ChannelID:  channelID,
		AccessHash: 0, // Initially set to 0; it will be populated by Telegram.
	}

	response, err := client.API().ChannelsGetChannels(ctx, []tg.InputChannelClass{inputChannel})
	if err != nil {
		return 0, fmt.Errorf("failed to get channel details: %v", err)
	}

	// Use GetChats() method to retrieve the list of chats
	chats := response.GetChats()
	if len(chats) == 0 {
		return 0, errors.New("no channel found with the given ID")
	}

	channel, ok := chats[0].(*tg.Channel)
	if !ok {
		return 0, errors.New("unexpected chat type")
	}

	return channel.AccessHash, nil
}
