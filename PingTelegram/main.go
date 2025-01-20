package main

import (
	"fmt"
	"log"
	"os"
	"os/signal"
	"syscall"

	"github.com/joho/godotenv"
	"github.com/kallazz/ping/telegram"
)

func main() {
	// Load .env if needed
	godotenv.Load()

	// Create the Telegram client
	client, err := telegram.NewClient()
	if err != nil {
		log.Fatal(err.Error())
	}
	fmt.Println("Telegram client initialized!")
	// Start receiving messages from the Ping gRPC server in a separate goroutine
	go func() {
		err := telegram.ReceiveMessagesFromPingGRPCServer(client.C)
		if err != nil {
			log.Printf("Error in receiving messages from gRPC server: %v\n", err)
		}
	}()

	// Now block until the Telegram client stops (Idle()) or user interrupts
	fmt.Println("Bot is now running. Press CTRL-C to exit.")
	sigC := make(chan os.Signal, 1)
	signal.Notify(sigC, syscall.SIGINT, syscall.SIGTERM)

	// You could either block on signals,
	// or you can rely on client.Idle() to block:
	go func() {
		// Wait for Telegram's Idle to finish
		if err := client.C.Idle(); err != nil {
			log.Printf("client.Idle() returned error: %v", err)
		}
	}()

	// Wait for termination signal
	<-sigC
	fmt.Println("Shutting down...")

	// Stop the Telegram client
	client.C.Stop()
}
