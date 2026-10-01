#!/usr/bin/env bash
# Resume o ROADMAP.md: caixas marcadas / total por seção (fase). Uso: tools/roadmap-status.sh [--pending]
set -euo pipefail
file="$(dirname "$0")/../ROADMAP.md"
awk -v pending="${1:-}" '
  /^## / { if (section != "") print_row(); section = $0; done = 0; total = 0; missing = "" ; next }
  /^- \[x\]/ { done++; total++ }
  /^- \[ \]/ { total++; sub(/^- \[ \] /, ""); missing = missing "    · " $0 "\n" }
  function print_row() {
    if (total > 0) {
      printf "%-70s %3d/%-3d\n", substr(section, 4), done, total
      if (pending == "--pending") printf "%s", missing
    }
  }
  END { print_row() }
' "$file"
total_done=$(grep -c '^- \[x\]' "$file" || true)
total_all=$(grep -c '^- \[[ x]\]' "$file" || true)
echo "---"
echo "Total: $total_done/$total_all itens concluídos"
