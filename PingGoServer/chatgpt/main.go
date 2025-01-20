// File: main.go
package main

import (
	"context"
	"fmt"
	"log"
	"net"
	"sync"
	"time"

	ping "github.com/kallazz/Ping/pb"
	"google.golang.org/grpc"
)

// pingServer will implement the PingServiceServer interface.
type pingServer struct {
	ping.UnimplementedPingServiceServer
	// In the C# code we have clientConnections and messageQueues as
	// ConcurrentDictionaries. Here we’ll do a map plus a mutex.
	mu sync.RWMutex

	// Map of userId -> streaming connection
	clientConnections map[string]ping.PingService_ReceiveMessagesServer

	// Map of userId -> queue of messages
	messageQueues map[string]chan *ping.ServerMessage
}

// NewPingServer creates and returns our server instance.
func NewPingServer() *pingServer {
	return &pingServer{
		clientConnections: make(map[string]ping.PingService_ReceiveMessagesServer),
		messageQueues:     make(map[string]chan *ping.ServerMessage),
	}
}

// SendMessage is analogous to SendMessage in C#.
func (s *pingServer) SendMessage(ctx context.Context, req *ping.MessageRequest) (*ping.ExitCode, error) {
	// "Database calls" omitted. We'll just do a simple pass-through:
	clientID := req.Client
	recipientID := req.Recipient
	message := req.Message

	fmt.Printf("Sending message to %s from %s: %s\n", recipientID, clientID, message)

	s.mu.Lock()
	defer s.mu.Unlock()
	_, ok := s.clientConnections[recipientID]
	if !ok {
		fmt.Printf("Recipient %s not connected.\n", recipientID)
		return &ping.ExitCode{Status: 1, Message: "Recipient not connected"}, nil
	}

	// Enqueue message
	msg := &ping.ServerMessage{
		MessageResponse: &ping.MessageResponse{
			Type:    "Message",
			Content: message,
			Sender:  clientID,
		},
		ExitCode: &ping.ExitCode{Status: 0, Message: "Message enqueued"},
	}
	s.messageQueues[recipientID] <- msg

	return &ping.ExitCode{Status: 0, Message: "Message sent"}, nil
}

// ProposeKeyExchange is analogous to ProposeKeyExchange in C#.
func (s *pingServer) ProposeKeyExchange(ctx context.Context, req *ping.KeyExchangeRequest) (*ping.ExitCode, error) {
	clientID := req.Client
	recipientID := req.Recipient
	publicKey := req.PublicKey
	init := req.Init

	fmt.Printf("Key exchange proposed from %s to %s\n", clientID, recipientID)

	s.mu.Lock()
	defer s.mu.Unlock()

	_, ok := s.clientConnections[recipientID]
	if !ok {
		fmt.Printf("Recipient %s not connected.\n", recipientID)
		return &ping.ExitCode{Status: 1, Message: "Recipient not connected"}, nil
	}

	exchangeType := "KeyExchangeResponse"
	if init {
		exchangeType = "KeyExchangeInit"
	}

	msg := &ping.ServerMessage{
		MessageResponse: &ping.MessageResponse{
			Type:    exchangeType,
			Content: string(publicKey), // or base64 if needed
			Sender:  clientID,
		},
		ExitCode: &ping.ExitCode{Status: 0, Message: "Key exchange forwarded"},
	}
	s.messageQueues[recipientID] <- msg

	return &ping.ExitCode{Status: 0, Message: "Key exchange proposed"}, nil
}

// ReceiveMessages corresponds to the streaming method where the server
// sends messages to the client.
func (s *pingServer) ReceiveMessages(req *ping.Empty, stream ping.PingService_ReceiveMessagesServer) error {
	clientID := req.Client
	if clientID == "" {
		return fmt.Errorf("client ID cannot be empty")
	}

	fmt.Printf("Client %s connected (stream)\n", clientID)

	// Setup a message queue for this client if not already existing
	s.mu.Lock()
	s.clientConnections[clientID] = stream
	if _, exists := s.messageQueues[clientID]; !exists {
		s.messageQueues[clientID] = make(chan *ping.ServerMessage, 100)
	}
	s.mu.Unlock()

	// Loop while context is alive, reading from the message channel
	for {
		select {
		case <-stream.Context().Done():
			// Client disconnected
			fmt.Printf("Client %s disconnected\n", clientID)
			s.mu.Lock()
			delete(s.clientConnections, clientID)
			delete(s.messageQueues, clientID)
			s.mu.Unlock()
			return nil

		case msg := <-s.messageQueues[clientID]:
			// In real code, you'd do DB lookups to convert sender ID to username, etc.
			// Here we just forward what we got.
			if err := stream.Send(msg); err != nil {
				fmt.Printf("Error sending message to %s: %v\n", clientID, err)
			}
		case <-time.After(100 * time.Millisecond):
			// Sleep to avoid busy-looping; adjust as needed
		}
	}
}

// Login corresponds to the Login method in the C# server.
func (s *pingServer) Login(ctx context.Context, req *ping.LoginRequest) (*ping.ExitCode, error) {
	// Do "auth" check, omitted for brevity
	// If successful:
	fmt.Printf("User %s logged in\n", req.Username)
	return &ping.ExitCode{Status: 0, Message: "Welcome to server"}, nil
}

// Register corresponds to the Register method in the C# server.
func (s *pingServer) Register(ctx context.Context, req *ping.RegisterRequest) (*ping.ExitCode, error) {
	// Do "registration" check, omitted for brevity
	// If successful:
	fmt.Printf("Registration success for user: %s\n", req.Username)
	return &ping.ExitCode{Status: 0, Message: "Welcome to server"}, nil
}

// GetFriends is analogous to GetFriends in C#.
func (s *pingServer) GetFriends(ctx context.Context, req *ping.FriendListRequest) (*ping.ServerMessage, error) {
	// For simplicity, pretend user has 2 friends:
	friendList := "alice;bob"
	msg := &ping.ServerMessage{
		MessageResponse: &ping.MessageResponse{Content: friendList},
		ExitCode:        &ping.ExitCode{Status: 0},
	}
	fmt.Printf("Returning friends for %s: %s\n", req.Client, friendList)
	return msg, nil
}

// AddFriend is analogous to AddFriend in C#.
func (s *pingServer) AddFriend(ctx context.Context, req *ping.AddFriendRequest) (*ping.ExitCode, error) {
	// For simplicity, always succeed:
	fmt.Printf("Friend added: %s -> %s\n", req.Client, req.Friend)
	return &ping.ExitCode{Status: 0, Message: "Friend added successfully"}, nil
}

// main function sets up and starts the gRPC server.
func main() {
	// Typically you'd load environment variables for port, etc.
	port := "50051"
	fmt.Printf("Starting server on port %s...\n", port)

	lis, err := net.Listen("tcp", ":"+port)
	if err != nil {
		log.Fatalf("Failed to listen: %v", err)
	}

	grpcServer := grpc.NewServer()
	srv := NewPingServer()

	// Register our pingServer as PingServiceServer
	ping.RegisterPingServiceServer(grpcServer, srv)

	// Start serving
	if err := grpcServer.Serve(lis); err != nil {
		log.Fatalf("Failed to serve: %v", err)
	}
}
