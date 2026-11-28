#!/bin/sh
# Creates Halima Yusuf, the connected journey's fictional young person, as a local Keycloak user in the yoma realm:
# halima.yusuf@example.org / P@ssword12 (local demo only, like the other test sign-ins), email verified, role User.
# Runs in the keycloak-users container (curl, with that service's KC_* settings), as admin-partner-role.sh does; start.sh
# --journey calls it. Her Yoma account row comes from generate-journey-sql.mjs and links to this user by email on her first
# sign-in. Idempotent: an existing Halima keeps her id and gets the password and role again.
set -eu

HALIMA_EMAIL="${HALIMA_EMAIL:-halima.yusuf@example.org}"
HALIMA_PASSWORD="${HALIMA_PASSWORD:-P@ssword12}" # public local default, as in src/api/docker-compose.yml

field() { sed -n "s/.*\"$1\":\"\\([^\"]*\\)\".*/\\1/p"; }
first_id() { sed -n 's/^\[{"id":"\([^"]*\)".*/\1/p'; }

jwt=$(curl -fsS -X POST "${KC_BASE_URL}/realms/${KC_ADMIN_REALM:-master}/protocol/openid-connect/token" \
  -d grant_type=password -d "client_id=${KC_CLIENT_ID}" -d "username=${KC_ADMIN_USER}" -d "password=${KC_ADMIN_PASSWORD}" | field access_token)
users="${KC_BASE_URL}/admin/realms/${KC_REALM}/users"

user_id=$(curl -fsS "${users}?exact=true&email=${HALIMA_EMAIL}" -H "Authorization: bearer ${jwt}" | first_id)
if [ -z "$user_id" ]; then
  # Attributes as users.sh gives the other test users, with Halima's own values (shared mock data v1.1: 20, Kaduna, Nigeria).
  curl -fsS -X POST "$users" -H "Authorization: bearer ${jwt}" -H 'Content-Type: application/json' -d '{
      "username": "'"${HALIMA_EMAIL}"'",
      "email": "'"${HALIMA_EMAIL}"'",
      "enabled": true,
      "emailVerified": true,
      "firstName": "Halima",
      "lastName": "Yusuf",
      "attributes": {
        "dateOfBirth": ["01/06/2006"],
        "country": ["Nigeria"],
        "gender": ["Female"],
        "education": ["Secondary (Grade 8–12, Matric or equivalent)"],
        "terms_and_conditions": ["Yes"]
      }
    }'
  user_id=$(curl -fsS "${users}?exact=true&email=${HALIMA_EMAIL}" -H "Authorization: bearer ${jwt}" | first_id)
  echo "Created ${HALIMA_EMAIL}"
else
  echo "${HALIMA_EMAIL} is already here"
fi
[ -n "$user_id" ] || { echo "Could not find ${HALIMA_EMAIL} after creating it" >&2; exit 1; }

curl -fsS -X PUT "${users}/${user_id}/reset-password" -H "Authorization: bearer ${jwt}" -H 'Content-Type: application/json' \
  -d '{"type":"password","value":"'"${HALIMA_PASSWORD}"'","temporary":false}'
role=$(curl -fsS "${KC_BASE_URL}/admin/realms/${KC_REALM}/roles/User" -H "Authorization: bearer ${jwt}")
curl -fsS -X POST "${users}/${user_id}/role-mappings/realm" -H "Authorization: bearer ${jwt}" -H 'Content-Type: application/json' -d "[${role}]"
echo "${HALIMA_EMAIL} can sign in (role User, email verified)"
