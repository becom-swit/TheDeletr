# TheDeletr

Console app that deletes files last modified on or before a given date from a folder (local or network share) and all its subfolders. Built with .NET 10, System.CommandLine, Spectre.Console and Serilog (audit log).

## Build & test

```powershell
dotnet build thedeletr.slnx
dotnet test thedeletr.slnx
dotnet publish src\Thedeletr -c Release -o publish
```

## Usage

```
TheDeletr <root> -d <date> [options]
```

| Option | Description |
|---|---|
| `<root>` | Root folder (local path or UNC share). Required. |
| `-d`, `--deletion <date>` | Cutoff date (`yyyy-MM-dd [HH:mm[:ss]]` or current culture). Files with last modified `<=` date are deleted. A date without a time means `00:00:00`. Required. |
| `-dr`, `--dryRun` | Only list the candidates. |
| `-e`, `--excluded <patterns>` | Comma/semicolon separated exclusion patterns. Can be repeated. |
| `-ef`, `--excludedFile <file>` | File with exclusion patterns (newline, comma or semicolon separated). |
| `-y`, `--yes` | Delete without confirmation (required for non-interactive runs). |
| `-b`, `--bin` | Move to recycle bin instead of permanent delete (Windows only; network shares have no recycle bin → permanent). |
| `-v`, `--verbose` | Don't list each candidate on the console, only the summary. |
| `-l`, `--logFolder <folder>` | Audit log folder. Default: `logs` next to the executable. |
| `-h`, `--help` | Help. |

### Exclusion patterns

Matched case-insensitively against folders **and** files. An excluded folder is not scanned at all.

| Pattern | Matches |
|---|---|
| `node_modules` | any folder/file named exactly `node_modules` |
| `temp*`, `*.bak`, `*cache*` | names starting with / ending with / containing the text |
| `C:\data\keep`, `\\srv\share\keep` | exactly this full path |
| `projects\keep` | path relative to the root folder |

`*` wildcards can also be used inside path patterns (e.g. `projects\*\bin`).

### Behaviour

- Junctions/symbolic-link folders are never followed.
- Read-only files are not deleted and are counted as failed.
- Files that were changed or removed between scan and deletion are skipped.
- Folders that become empty because of the deletion are removed (never the root, excluded folders or folders that were already empty).
- Ctrl+C cancels the run; folders are not cleaned up after a cancel.

### Exit codes

| Code | Meaning |
|---|---|
| 0 | Success (incl. dry run, nothing to delete, aborted at confirmation) |
| 1 | Invalid arguments / non-interactive without `--yes` / log folder not writable |
| 2 | Completed with failures (scan errors, failed files or folders) |
| 3 | Cancelled |

### Audit log

Daily rolling file `TheDeletr-YYYYMMDD.log`, kept 90 days. Each run has a run id and logs user, machine, all parameters, every candidate, exclusion, deletion, skip and failure, plus a final summary. The audit log is always complete, even with `-v`.

## Examples

```powershell
# preview
TheDeletr \\server\share\exports -d 2024-01-01 --dryRun -e "archive;*.keep"

# unattended (e.g. scheduled task)
TheDeletr D:\data\tmp -d 2024-06-30 -ef exclusions.txt -y -v

# to recycle bin, with confirmation
TheDeletr C:\Users\me\Downloads -d "2024-01-01 12:00" -b
```
