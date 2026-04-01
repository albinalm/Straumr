$_straumrCompleter = {
    param($wordToComplete, $commandAst, $cursorPosition)
    $cmdName = $commandAst.CommandElements[0].ToString()
    $query = ($commandAst.ToString() -replace "^$([regex]::Escape($cmdName))\s*", '')
    if ($commandAst.ToString().EndsWith(' ')) { $query = "$query " }
    straumr autocomplete query $query 2>$null | ForEach-Object {
        [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_)
    }
}
