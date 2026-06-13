# Git Integration Guide

RackPeek can automatically save and sync its configuration with any Git host
that accepts a personal access token over HTTPS — GitHub, Gitea, GitLab,
Bitbucket, Forgejo, self-hosted, etc. RackPeek does not use any host-specific
APIs; the integration is plain `git` over HTTPS.

## What you need

1. **A repository** on your chosen Git host, dedicated to storing the
   RackPeek configuration. It can start empty.
2. **A personal access token** with permission to push to that repository.
3. **The username** the token authenticates as.

### Where to create a token

* **GitHub** — Settings → Developer settings → Personal access tokens →
  Fine-grained tokens. Select the target repository and grant
  **Contents: Read and Write**.
* **Gitea / Forgejo** — Settings → Applications → Generate New Token.
  Scope: `write:repository`.
* **GitLab** — User Settings → Access Tokens. Scope: `write_repository`.
* **Bitbucket** — Personal settings → App passwords → Create app password
  with `Repositories: Read, Write`.

Copy the token immediately — most hosts only show it once.

## Configuring RackPeek

Provide the token and username to the container via environment variables:

| Variable | Required | Description |
|---|---|---|
| `GIT_TOKEN` | Yes | Personal access token (used as the password for HTTP Basic auth). |
| `GIT_USERNAME` | Yes | The username the token belongs to. |
| `GIT_INSECURE_TLS` | No | Set to `true` to skip TLS certificate validation — useful when pointing at a self-hosted Gitea / GitLab behind a private CA or self-signed certificate. Leave unset for any public host. |

Example with Docker Compose, pointing at a self-hosted Gitea:

```yaml
version: "3.9"

services:
  rackpeek:
    image: aptacode/rackpeek:latest
    container_name: rackpeek
    ports:
      - "8080:8080"
    volumes:
      - rackpeek-config:/app/config
    environment:
      - GIT_TOKEN=your_token_here
      - GIT_USERNAME=your_username
      # Uncomment the next line only if your Git host uses a private CA
      # or a self-signed certificate (common for home-lab Gitea instances):
      # - GIT_INSECURE_TLS=true
    restart: unless-stopped

volumes:
  rackpeek-config:
```

Example with the Docker CLI:

```bash
docker run -d \
  --name rackpeek \
  -p 8080:8080 \
  -v rackpeek-config:/app/config \
  -e GIT_TOKEN=your_token_here \
  -e GIT_USERNAME=your_username \
  aptacode/rackpeek:latest
```

## Wiring up the remote

Open RackPeek in the browser. With `GIT_TOKEN` set, a Git status indicator
appears in the header. RackPeek initialises a local git repository inside
the config directory automatically on startup — no extra click required.
Click **Add Remote** in the indicator and enter the repository URL — for
example:

* `https://github.com/youruser/rackpeek-config.git`
* `https://gitea.example.com/youruser/rackpeek-config.git`
* `https://gitlab.example.com/youruser/rackpeek-config.git`

RackPeek will commit and sync configuration changes from there on.

If the indicator shows **"Git configured but config directory is not
writable"**, the container can't create the `.git/` folder inside
`/app/config`. This is almost always a host filesystem permission issue
on a bind mount — see the [Installation Guide](install-guide) for the
ownership fix (`chown -R 1000:1000 /path/on/host/rackpeek`).

## Security notes

* The token is read from the environment at container start; it is not
  persisted in the YAML config.
* `GIT_INSECURE_TLS=true` disables certificate validation on push, pull,
  and fetch. Only set it on networks you trust. Public hosts (github.com,
  gitlab.com, gitea.com, …) ship correct, trusted certificates — you do
  not need this flag for them.
