package telegram

import (
	"errors"
	"os"
	"strconv"

	"github.com/celestix/gotgproto"
	"github.com/celestix/gotgproto/dispatcher/handlers"
	"github.com/celestix/gotgproto/sessionMaker"
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
		return nil, errors.New("Failed to create the telegram client.")
	}

	clientDispatcher := client.Dispatcher

	clientDispatcher.AddHandler(handlers.NewCommand("start"))
	client.Idle()
	//fmt.Println(client)

	return &Client{
		C: client,
	}, nil
}
