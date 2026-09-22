_straumr_completion() {
    local query="${COMP_LINE#${COMP_WORDS[0]} }"
    local current="${COMP_WORDS[COMP_CWORD]}"
    local candidate
    COMPREPLY=()

    while IFS= read -r candidate; do
        [[ "$candidate" == "$current"* ]] && COMPREPLY+=("$candidate")
    done < <(straumr autocomplete query "$query" 2>/dev/null)
}
