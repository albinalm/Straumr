package cache

import (
	"sync"
	"time"
)

type entry struct {
	value     any
	expiresAt time.Time
	hasExpiry bool
}

type Store struct {
	mu      sync.RWMutex
	entries map[string]entry
}

func NewStore() *Store {
	return &Store{
		entries: make(map[string]entry),
	}
}

func (s *Store) Get(key string) (any, bool) {
	s.mu.RLock()
	entry, ok := s.entries[key]
	s.mu.RUnlock()
	if !ok {
		return nil, false
	}

	if entry.hasExpiry && time.Now().After(entry.expiresAt) {
		s.Delete(key)
		return nil, false
	}

	return entry.value, true
}

func (s *Store) Set(key string, value any, ttl time.Duration) {
	s.mu.Lock()
	defer s.mu.Unlock()

	item := entry{value: value}
	if ttl > 0 {
		item.hasExpiry = true
		item.expiresAt = time.Now().Add(ttl)
	}

	s.entries[key] = item
}

func (s *Store) Delete(key string) {
	s.mu.Lock()
	delete(s.entries, key)
	s.mu.Unlock()
}

func (s *Store) Clear() {
	s.mu.Lock()
	s.entries = make(map[string]entry)
	s.mu.Unlock()
}
