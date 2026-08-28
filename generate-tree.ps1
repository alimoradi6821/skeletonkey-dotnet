param (
    [string]$OutputFile = "project-structure.txt"
)

$PIPE = [char]0x2502
$TEE  = [char]0x251C
$ELBOW = [char]0x2514
$DASH = [char]0x2500

function Write-Tree {
    param (
        [string]$Path = ".",
        [string]$Prefix = "",
        [System.IO.StreamWriter]$Writer
    )

    $items = Get-ChildItem -LiteralPath $Path -Force |
        Where-Object {
            $_.Name -notmatch '^\.' -and
            $_.Name -ne "__pycache__" -and
            $_.Name -ne "artifacts" -and
            $_.Name -ne "bin" -and
            $_.Name -ne "obj" -and
            $_.Name -ne ".git"
        } |
        Sort-Object `
            @{Expression = { -not $_.PSIsContainer }}, `
            @{Expression = { $_.Name }}

    for ($i = 0; $i -lt $items.Count; $i++) {

        $item = $items[$i]
        $isLast = ($i -eq $items.Count - 1)

        if ($isLast) {
            $connector = "$ELBOW$DASH$DASH "
        } else {
            $connector = "$TEE$DASH$DASH "
        }

        $line = "$Prefix$connector$($item.Name)"
        $Writer.WriteLine($line)

        if ($item.PSIsContainer) {
            if ($isLast) {
                $newPrefix = "$Prefix    "
            } else {
                $newPrefix = "$Prefix$PIPE   "
            }

            Write-Tree -Path $item.FullName -Prefix $newPrefix -Writer $Writer
        }
    }
}

$fullPath = Join-Path (Get-Location) $OutputFile
$writer = [System.IO.StreamWriter]::new($fullPath,$false,[System.Text.Encoding]::UTF8)

try {
    Write-Tree -Writer $writer
}
finally {
    $writer.Close()
}

Write-Host "✅ structure saved to $OutputFile"
