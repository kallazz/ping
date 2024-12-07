package main

import (
	"fmt"
	"log"

	"github.com/celestix/gotgproto/dispatcher"
	"github.com/celestix/gotgproto/ext"
	"github.com/gotd/td/tg"
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

// callback function for /start command
func start(ctx *ext.Context, update *ext.Update) error {
	user := update.EffectiveUser()
	_, _ = ctx.Reply(update, ext.	(fmt.Sprintf("Hello %s, I am @%s and will repeat all your messages.\nI was made using gotd and gotgproto.", user.FirstName, ctx.Self.Username)), &ext.ReplyOpts{
		Markup: &tg.ReplyInlineMarkup{
			Rows: []tg.KeyboardButtonRow{
				{
					Buttons: []tg.KeyboardButtonClass{
						&tg.KeyboardButtonURL{
							Text: "gotd/td",
							URL:  "https://github.com/gotd/td",
						},
						&tg.KeyboardButtonURL{
							Text: "gotgproto",
							URL:  "https://github.com/celestix/gotgproto",
						},
					},
				},
				{
					Buttons: []tg.KeyboardButtonClass{
						&tg.KeyboardButtonCallback{
							Text: "Click Here",
							Data: []byte("cb_pressed"),
						},
					},
				},
			},
		},
	})
	// End dispatcher groups so that bot doesn't echo /start command usage
	return dispatcher.EndGroups
}

func buttonCallback(ctx *ext.Context, update *ext.Update) error {
	query := update.CallbackQuery
	_, _ = ctx.AnswerCallback(&tg.MessagesSetBotCallbackAnswerRequest{
		Alert:   true,
		QueryID: query.QueryID,
		Message: "This is an example bot!",
	})
	return nil
}

func echo(ctx *ext.Context, update *ext.Update) error {
	msg := update.EffectiveMessage
	_, err := ctx.Reply(update, ext.ReplyTextString(msg.Text), nil)
	return err
}
