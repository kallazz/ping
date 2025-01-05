package main

import (
	"context"
	"fmt"
	"log"
	"net"

	ping "github.com/kallazz/Ping/PingDiscord/pb"
	"google.golang.org/grpc"
)

type Server struct {
	ping.UnimplementedPingServiceServer
}

func (s *Server) SendMessage(ctx context.Context, in *ping.MessageRequest) (*ping.ExitCode, error) {
	fmt.Println("Szuruburu processing data beep boop beep boop")

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
	ping.RegisterPingServiceServer(s, &Server{})
	log.Printf("gRPC server listening at %v", lis.Addr())
	if err := s.Serve(lis); err != nil {
		log.Fatalf("failed to serve: %v", err)
	}
}
