package telegram

import (
	"context"
	"errors"
	"fmt"
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
	client.Idle()
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
	conn, err := grpc.NewClient("localhost:50051", grpc.WithTransportCredentials(insecure.NewCredentials()))
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
