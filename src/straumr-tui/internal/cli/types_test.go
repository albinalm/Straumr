package cli

import (
	"encoding/json"
	"testing"
)

func TestFlexibleTimeUnmarshalAcceptsTimezoneLessTimestamp(t *testing.T) {
	var result WorkspaceGetResult
	payload := []byte(`{
		"Id":"ws-1",
		"Name":"demo",
		"Modified":"2026-04-17T08:04:34",
		"LastAccessed":"2026-04-17T08:04:34"
	}`)

	if err := json.Unmarshal(payload, &result); err != nil {
		t.Fatalf("expected timezone-less timestamp to decode, got error: %v", err)
	}

	if got := result.Modified.Format("2006-01-02 15:04:05"); got != "2026-04-17 08:04:34" {
		t.Fatalf("unexpected modified timestamp: %s", got)
	}
}

func TestFlexibleTimeUnmarshalAcceptsRFC3339Timestamp(t *testing.T) {
	var result struct {
		LastAccessed FlexibleTime `json:"LastAccessed"`
	}

	if err := json.Unmarshal([]byte(`{"LastAccessed":"2026-04-17T08:04:34Z"}`), &result); err != nil {
		t.Fatalf("expected RFC3339 timestamp to decode, got error: %v", err)
	}

	if result.LastAccessed.IsZero() {
		t.Fatal("expected parsed timestamp, got zero value")
	}
}
