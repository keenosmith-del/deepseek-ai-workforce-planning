# Optional OpenTofu Docker deployment

This provisions a complete, isolated service network on an existing Docker host. It needs no paid cloud account. Use Docker Compose for the local demo. The provider can connect to a remote Linux host over SSH (`ssh://operator@host`), or local Docker's socket. Install OpenTofu >=1.8 and run:

```sh
tofu -chdir=infra init
tofu -chdir=infra fmt
tofu -chdir=infra validate
tofu -chdir=infra plan -var-file=deployment.tfvars
tofu -chdir=infra apply -var-file=deployment.tfvars
```

Create `infra/deployment.tfvars` from the example; secret variables and Docker environment values are retained in state. Restrict state permissions and use encrypted remote state for shared deployment. The optional deployment disables demo seeding; initialize an HR administrator through the documented bootstrap command before disabling seeding, or temporarily enable seeding with new secrets on an isolated demo host. Containers use named persistent volumes and the web port binds loopback. Use an SSH tunnel to view a remote deployment, or add an authenticated TLS reverse proxy for wider use. Model inference defaults to the host's Ollama; install both models first. `tofu destroy` includes volume deletion; back up persistent data first.

Bootstrap an unseeded OpenTofu deployment using exported `Bootstrap__Email` and `Bootstrap__Password` values:

```sh
docker exec -e Bootstrap__Enabled=true -e Bootstrap__Email -e Bootstrap__Password \
  workforce-tofu-api dotnet Workforce.dll
```

This uses the existing container's database/configuration, refuses a second administrator bootstrap, and exits without binding another HTTP server.
