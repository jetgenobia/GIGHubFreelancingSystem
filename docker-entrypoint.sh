set -e

if [ -n "$WAIT_FOR_DB" ]; then
  echo "Waiting for DB..."
  sleep 5
fi

exec dotnet Freelancing.dll