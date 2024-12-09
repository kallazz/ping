package main

import (
	"fmt"
	"log"

	"github.com/joho/godotenv"
	"github.com/kallazz/ping/telegram"
)

func main() {
	godotenv.Load()

	client, err := telegram.NewClient()
	if err != nil {
		log.Fatal(err.Error())
	}

	fmt.Println("Have a client!")
	fmt.Println(client)
}
