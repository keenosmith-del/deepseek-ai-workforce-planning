#!/usr/bin/env sh
set -eu
docker compose --profile models up -d ollama
docker compose exec -T ollama ollama pull "${OLLAMA_MODEL:-deepseek-r1:1.5b}"
docker compose exec -T ollama ollama pull "${EMBED_MODEL:-nomic-embed-text}"
