package dialogs

// OverlayState is the shared open/close lifecycle used by the owned overlay
// views. The root model can decide when to mount or unmount the overlay, while
// the view package keeps the local state transitions explicit.
type OverlayState struct {
	Active bool
}

func (s *OverlayState) Open() {
	s.Active = true
}

func (s *OverlayState) Close() {
	s.Active = false
}

type SelectionResult struct {
	Accepted  bool
	Cancelled bool
	Index     int
	Choice    Choice
	Filter    string
}

type ConfirmResult struct {
	Accepted  bool
	Cancelled bool
	Index     int
	Choice    string
}

type InputResult struct {
	Accepted  bool
	Cancelled bool
	Value     string
}

type KeyValueResult struct {
	Accepted  bool
	Cancelled bool
	Index     int
	Item      Pair
	Items     []Pair
}

type PathResult struct {
	Accepted  bool
	Cancelled bool
	Mode      PathMode
	Path      string
	Entry     PathEntry
}
