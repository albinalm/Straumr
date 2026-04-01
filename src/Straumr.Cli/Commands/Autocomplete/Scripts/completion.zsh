_straumr_complete() {
    local query="${BUFFER#${words[1]} }"
    local completions
    completions=$(straumr autocomplete query "$query" 2>/dev/null) || return
    compadd -Q -- ${(f)completions}
}
