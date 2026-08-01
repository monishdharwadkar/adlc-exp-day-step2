#!/bin/sh
set -eu

# Runtime placeholder replacement for the Vite API base URL.
# The built index.html contains the token `__VITE_API_URL__`.
escaped_api_url="$(printf '%s' "${VITE_API_URL:-}" | sed -e 's/[\/&]/\\&/g')"
sed -i "s|__VITE_API_URL__|$escaped_api_url|g" /usr/share/nginx/html/index.html

exec nginx -g 'daemon off;'
