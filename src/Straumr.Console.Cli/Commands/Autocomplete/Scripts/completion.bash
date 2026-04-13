_straumr_completion() {
    local query="${COMP_LINE#${COMP_WORDS[0]} }"
    local completions
    completions=$(straumr autocomplete query "$query" 2>/dev/null) || return
    COMPREPLY=($(compgen -W "$completions" -- "${COMP_WORDS[COMP_CWORD]}"))
}
