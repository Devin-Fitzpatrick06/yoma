#!/bin/sh
# Gives the local demo admin the OrganisationAdmin role too, so one sign-in reaches the learner, reviewer and partner views.
# Runs in the keycloak-users container (curl, with that service's KC_* settings). Local demo only.
set -eu

field() { sed -n "s/.*\"$1\":\"\\([^\"]*\\)\".*/\\1/p"; }

jwt=$(curl -fsS -X POST "${KC_BASE_URL}/realms/${KC_ADMIN_REALM:-master}/protocol/openid-connect/token" \
  -d grant_type=password -d "client_id=${KC_CLIENT_ID}" -d "username=${KC_ADMIN_USER}" -d "password=${KC_ADMIN_PASSWORD}" | field access_token)

role=$(curl -fsS "${KC_BASE_URL}/admin/realms/${KC_REALM}/roles/OrganisationAdmin" -H "Authorization: bearer ${jwt}")
user_id=$(curl -fsS "${KC_BASE_URL}/admin/realms/${KC_REALM}/users?exact=true&email=${ADMIN_USER}" -H "Authorization: bearer ${jwt}" | sed 's/^\[{"id":"\([^"]*\)".*/\1/')

curl -fsS -X POST "${KC_BASE_URL}/admin/realms/${KC_REALM}/users/${user_id}/role-mappings/realm" \
  -H "Authorization: bearer ${jwt}" -H 'Content-Type: application/json' -d "[${role}]"
echo "${ADMIN_USER} is now also an organisation admin"
