package theme

type ThemeOptions struct {
	Theme Theme `json:"Theme"`
}

type Theme struct {
	Surface        string `json:"Surface"`
	SurfaceVariant string `json:"SurfaceVariant"`
	OnSurface      string `json:"OnSurface"`
	Primary        string `json:"Primary"`
	OnPrimary      string `json:"OnPrimary"`
	Secondary      string `json:"Secondary"`
	Accent         string `json:"Accent"`
	Success        string `json:"Success"`
	Info           string `json:"Info"`
	Warning        string `json:"Warning"`
	Danger         string `json:"Danger"`
	MethodGet      string `json:"MethodGet"`
	MethodPost     string `json:"MethodPost"`
	MethodPut      string `json:"MethodPut"`
	MethodPatch    string `json:"MethodPatch"`
	MethodDelete   string `json:"MethodDelete"`
	MethodHead     string `json:"MethodHead"`
	MethodOptions  string `json:"MethodOptions"`
	MethodTrace    string `json:"MethodTrace"`
	MethodConnect  string `json:"MethodConnect"`
}

func Default() Theme {
	return Theme{
		Surface:        "",
		SurfaceVariant: "DarkGray",
		OnSurface:      "White",
		Primary:        "BrightGreen",
		OnPrimary:      "Black",
		Secondary:      "Gray",
		Accent:         "BrightGreen",
		Success:        "BrightGreen",
		Info:           "BrightBlue",
		Warning:        "Yellow",
		Danger:         "Red",
		MethodGet:      "BrightBlue",
		MethodPost:     "BrightGreen",
		MethodPut:      "Yellow",
		MethodPatch:    "BrightCyan",
		MethodDelete:   "Red",
		MethodHead:     "Gray",
		MethodOptions:  "Gray",
		MethodTrace:    "Gray",
		MethodConnect:  "Gray",
	}
}
