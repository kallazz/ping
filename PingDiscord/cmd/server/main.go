package main

import (
	"context"
	"fmt"
	"log"
	"net"
	"sync"
	"time"

	ping "github.com/kallazz/Ping/PingDiscord/pb"
	"google.golang.org/grpc"
)

type Server struct {
	ping.UnimplementedPingServiceServer
	clientStreams map[string]ping.PingService_ReceiveMessagesServer // Map to store client streams
	mu            sync.Mutex
}

// func (s *Server) ReceiveMessages(ctx context.Context) (*ping.ServerMessage, error) {
//
// }

func (s *Server) ReceiveMessages(req *ping.Empty, stream ping.PingService_ReceiveMessagesServer) error {
	clientID := req.Client
	fmt.Printf("Client %s connected to ReceiveMessages\n", clientID)

	// Add client stream to the map
	s.mu.Lock()
	s.clientStreams[clientID] = stream
	s.mu.Unlock()

	// Keep the connection open
	for {
		select {
		case <-stream.Context().Done():
			fmt.Printf("Client %s disconnected from ReceiveMessages\n", clientID)
			s.mu.Lock()
			delete(s.clientStreams, clientID)
			s.mu.Unlock()
			return nil
		default:
			time.Sleep(1 * time.Second) // Keep the loop running
		}
	}
}

// Function to broadcast a message to all connected clients
func (s *Server) broadcastMessage(msg *ping.ServerMessage) {
	s.mu.Lock()
	defer s.mu.Unlock()

	for clientID, stream := range s.clientStreams {
		fmt.Printf("Sending message to client %s\n", clientID)
		if err := stream.Send(msg); err != nil {
			fmt.Printf("Error sending message to client %s: %v\n", clientID, err)
			delete(s.clientStreams, clientID) // Remove disconnected clients
		}
	}
}

func (s *Server) SendMessage(ctx context.Context, in *ping.MessageRequest) (*ping.ExitCode, error) {
	fmt.Println("Szuruburu processing data beep boop beep boop")

	s.broadcastMessage(&ping.ServerMessage{
		MessageResponse: &ping.MessageResponse{
			Type:    "Broadcast",
			Content: in.Message,
			Sender:  in.Client,
		},
	})

	return &ping.ExitCode{
		Status:  1,
		Message: fmt.Sprintf("Succesfuly processed: %v", in.Message),
	}, nil
}

func main() {
	lis, err := net.Listen("tcp", ":50051")
	if err != nil {
		log.Fatalf("Failed to listen on port 50051: %v", err)
	}

	s := grpc.NewServer()
	server := &Server{
		clientStreams: make(map[string]ping.PingService_ReceiveMessagesServer), // Initialize the map
	}
	ping.RegisterPingServiceServer(s, server)
	log.Printf("gRPC server listening at %v", lis.Addr())
	if err := s.Serve(lis); err != nil {
		log.Fatalf("failed to serve: %v", err)
	}
}
