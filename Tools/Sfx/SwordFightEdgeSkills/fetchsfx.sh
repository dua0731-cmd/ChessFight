# usage: fetchsfx.sh <status-result-file> <sessions.txt> <outdir> ; downloads every listed session's mp3 found in the file
F="$1"; M="$2"; O="$3"; mkdir -p "$O"
grep -o 'https://storage.googleapis.com/xi-backend/database/workspace/[^"\]*content_generation/[A-Za-z0-9]*/[A-Za-z0-9]*/content.mp3?[^"\]*' "$F" | sort -u | while read u; do
  s=$(echo "$u" | sed -E 's#.*/content_generation/([A-Za-z0-9]+)/.*#\1#'); n=$(grep "^$s " "$M" | cut -d' ' -f2)
  [ -n "$n" ] && [ ! -s "$O/$n.mp3" ] && curl -s -o "$O/$n.mp3" "$u" && echo "got $n"
done
