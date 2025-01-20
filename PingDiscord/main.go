package main

import (
	"context"
	"flag"
	"fmt"
	"os"
	"os/signal"
	"syscall"
	"time"
	"strings"

	"github.com/bwmarrin/discordgo"
	ping "github.com/kallazz/Ping/PingDiscord/pb"
	"google.golang.org/grpc"
	"github.com/joho/godotenv"
	"google.golang.org/grpc/credentials/insecure"
)

var (
	Token string
)

func init() {
	flag.StringVar(&Token, "t", "", "Bot Token")
	flag.Parse()

    if err := godotenv.Load(); err != nil {
        fmt.Println("Error loading .env file")
        os.Exit(1)
    }
}

func main() {

	dg, err := discordgo.New("Bot " + Token)
	if err != nil {
		fmt.Println("error creating Discord session,", err)
		return
	}
	dg.AddHandler(messageCreate)
	dg.Identify.Intents = discordgo.IntentsGuildMessages

	// Open a websocket connection to Discord and begin listening.
	err = dg.Open()
	if err != nil {
		fmt.Println("error opening connection,", err)
		return
	}

	go receiveMessagesFromPingGRPCServer(dg)

	// Wait here until CTRL-C or other term signal is received.
	fmt.Println("Bot is now running.  Press CTRL-C to exit.")
	sc := make(chan os.Signal, 1)
	signal.Notify(sc, syscall.SIGINT, syscall.SIGTERM, os.Interrupt)
	<-sc

	// Cleanly close down the Discord session.
	dg.Close()
}

func messageCreate(s *discordgo.Session, m *discordgo.MessageCreate) {
	if m.Author.ID == s.State.User.ID {
		return
	}

	if m.Content == "ping" {
		s.ChannelMessageSend(m.ChannelID, "Pong!")
	}
	if m.Content == "pong" {
		s.ChannelMessageSend(m.ChannelID, "Ping!")
	}
	response, err := sendMessageToPingGRPCServer(m.Author.ID, m.ChannelID, m.Content)
	if err != nil {
		return
	}
	fmt.Printf("Response from ping server: %v\n", response)
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
	msgRequest.Client = "Discord"
	msgRequest.Author = author
	msgRequest.Recipient = recipient
	msgRequest.Message = message
	r, err := c.SendMessage(ctx, msgRequest)
	// log.Printf("Response from gRPC server's SayHello function: %s", r.GetMessage())
	return r.GetMessage(), nil
}

func receiveMessagesFromPingGRPCServer(dg *discordgo.Session) {
	host := os.Getenv("HOST")
    port := os.Getenv("PORT")
	address := fmt.Sprintf("%s:%s", host, port)
	conn, err := grpc.NewClient(address, grpc.WithTransportCredentials(insecure.NewCredentials()))
	if err != nil {
		fmt.Println("failed to connect with gRPC server:", err)
		return
	}
	defer conn.Close()

	c := ping.NewPingServiceClient(conn)
	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()

	// define Empty message

	connect_req := &ping.Empty{Client: "DiscordBot"}

	stream, err := c.ReceiveMessages(ctx, connect_req)
	if err != nil {
		fmt.Println("error starting gRPC stream:", err)
		return
	}

	for {
		msg, err := stream.Recv()
		if err != nil {
			fmt.Println("error receiving message from gRPC stream:", err)
			return
		}

		// Broadcast the received message to all Discord channels
		// if msg is already a Discord message, you can skip this step
		if msg.MessageResponse.Type != "Discord" && !strings.Contains(msg.MessageResponse.Content, "[Discord]") {
			fmt.Println("Broadcasting message to Discord:", msg.MessageResponse.Content)
			broadcastMessageToDiscord(dg, msg)
		}

		// micro sleep 
		time.Sleep(50 * time.Millisecond)
	}
}

func broadcastMessageToDiscord(dg *discordgo.Session, msg *ping.ServerMessage) {
	guilds := dg.State.Guilds
	for _, guild := range guilds {
		// Get the first available text channel in the guild
		channels, err := dg.GuildChannels(guild.ID)
		if err != nil {
			fmt.Println("error fetching channels for guild:", guild.ID, err)
			continue
		}

		for _, channel := range channels {
			if channel.Type == discordgo.ChannelTypeGuildText {
				fmt.Println("Broadcasting message to channel:", channel.ID)
				dg.ChannelMessageSend(channel.ID, fmt.Sprintf("[%s] %s: %s", msg.MessageResponse.Type, msg.MessageResponse.Sender, msg.MessageResponse.Content))
				break
			}
		}
	}
}
