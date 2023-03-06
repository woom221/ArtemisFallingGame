package main

import (
	"artemisFallingServer/backend"
	pb "artemisFallingServer/proto"
	"github.com/gorilla/websocket"
	"github.com/pion/webrtc/v3"
	"google.golang.org/protobuf/proto"
)

var (
	IceConfig webrtc.Configuration
)

func init() {
	IceConfig = webrtc.Configuration{
		ICEServers: []webrtc.ICEServer{
			{
				URLs: []string{"stun:stun.l.google.com:19302"},
			},
		},
	}
}

func connectServer(client *backend.Client, ws *websocket.Conn) error {
	peerConnection, err := webrtc.NewPeerConnection(IceConfig)
	if err != nil {
		return err
	}
	client.WRTC = peerConnection

	peerConnection.OnICECandidate(func(c *webrtc.ICECandidate) {
		if c == nil {
			return
		}

		candidate := c.ToJSON()
		resp := &pb.ICECandidateInit{
			Candidate:        candidate.Candidate,
			SDPMid:           candidate.SDPMid,
			UsernameFragment: candidate.UsernameFragment,
		}
		if candidate.SDPMLineIndex != nil {
			a := int32(*candidate.SDPMLineIndex)
			resp.SDPMLineIndex = &a
		}
		d, err := proto.Marshal(&pb.Trickle{Message: &pb.Trickle_Candidate{Candidate: resp}})
		if err != nil {
			log.Errorf("Error while marshaling message: %s", err.Error())
			return
		}
		if err = ws.WriteMessage(websocket.BinaryMessage, d); err != nil {
			return
		}
	})

	peerConnection.OnConnectionStateChange(func(state webrtc.PeerConnectionState) {
		log.Debug("state is now ", state)

		if state == webrtc.PeerConnectionStateConnected {
			client.Active = true
		}

		if state == webrtc.PeerConnectionStateFailed || state == webrtc.PeerConnectionStateDisconnected || state == webrtc.PeerConnectionStateClosed { // remove even a temp disconnected client
			server.removeClient(client.Id, "Disconnected")
		}
	})

	peerConnection.OnDataChannel(func(d *webrtc.DataChannel) {
		d.OnOpen(func() {
			switch d.Label() {
			case "priority":
				client.PriorityChannel = d
			case "fast":
				client.FastChannel = d
			default:
				log.Debug("unknown datachannel type", d.Label())
			}
		})

		d.OnMessage(server.parseMessage(d.Label(), client))
	})

	for {
		// Read each inbound WebSocket Message
		_, msg, err := ws.ReadMessage()
		if e, ok := err.(*websocket.CloseError); ok {
			log.Debug(e)
			return nil
		} else if err != nil {
			return err
		}

		var data = new(pb.Trickle)
		err = proto.Unmarshal(msg, data)
		if err != nil {
			return err
		}

		switch data.Message.(type) {
		case *pb.Trickle_Description:
			desc := data.GetDescription()
			offer := webrtc.SessionDescription{
				Type: webrtc.SDPType(desc.GetType() + 1),
				SDP:  desc.GetSDP(),
			}

			if err = peerConnection.SetRemoteDescription(offer); err != nil {
				return err
			}

			answer, answerErr := peerConnection.CreateAnswer(nil)
			if answerErr != nil {
				return answerErr
			}

			if err = peerConnection.SetLocalDescription(answer); err != nil {
				return err
			}

			resp := &pb.Trickle{Message: &pb.Trickle_Description{Description: &pb.SessionDescription{
				Type: int32(answer.Type) - 1,
				SDP:  answer.SDP,
			}}}
			d, err := proto.Marshal(resp)
			if err != nil {
				log.Errorf("Error while marshaling message: %s", err.Error())
				return err
			}

			if err = ws.WriteMessage(websocket.BinaryMessage, d); err != nil {
				return err
			}

		case *pb.Trickle_Candidate:
			candidate := data.GetCandidate()
			var idx *uint16
			if candidate.SDPMLineIndex != nil {
				a := uint16(candidate.GetSDPMLineIndex())
				idx = &a
			}
			if err = peerConnection.AddICECandidate(webrtc.ICECandidateInit{
				Candidate:        candidate.GetCandidate(),
				SDPMid:           candidate.SDPMid,
				SDPMLineIndex:    idx,
				UsernameFragment: candidate.UsernameFragment,
			}); err != nil {
				return err
			}
		default:
			log.Error("Unknown message", msg)
			return nil
		}
	}
}
