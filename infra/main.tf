terraform {
  required_version = ">= 1.8.0"
  required_providers {
    docker = {
      source = "kreuzwerker/docker", version = "~> 3.6"
    }

  }
}
provider "docker" {
  host = var.docker_host
}
variable "docker_host" {
  type    = string
  default = "unix:///var/run/docker.sock"
}
variable "project_root" {
  type        = string
  description = "Absolute checkout path on the machine running OpenTofu"
}
variable "database_password" {
  type      = string
  sensitive = true
}
variable "jwt_key" {
  type      = string
  sensitive = true
}
variable "internal_key" {
  type      = string
  sensitive = true
}
variable "ollama_url" {
  type    = string
  default = "http://host.docker.internal:11434"
}
variable "web_port" {
  type    = number
  default = 5180
}
resource "docker_network" "workforce" {
  name = "workforce-tofu"
}
resource "docker_volume" "database" {
  name = "workforce-tofu-db"
}
resource "docker_volume" "documents" {
  name = "workforce-tofu-documents"
}
resource "docker_image" "db" {
  name         = "pgvector/pgvector:pg16"
  keep_locally = true
}
resource "docker_image" "alpine" {
  name         = "alpine:3.21"
  keep_locally = true
}
resource "docker_image" "application" {
  for_each = toset(["backend", "ai", "frontend"])
  name     = "workforce-tofu-${each.key}:local"
  build {
    context = "${var.project_root}/${each.key}"
  }
}
resource "docker_container" "db" {
  name  = "workforce-tofu-db"
  image = docker_image.db.image_id
  env   = ["POSTGRES_DB=workforce", "POSTGRES_USER=workforce", "POSTGRES_PASSWORD=${var.database_password}"]
  networks_advanced {
    name    = docker_network.workforce.name
    aliases = ["db"]
  }
  volumes {
    volume_name    = docker_volume.database.name
    container_path = "/var/lib/postgresql/data"
  }
  healthcheck {
    test     = ["CMD-SHELL", "pg_isready -U workforce -d workforce"]
    interval = "3s"
    retries  = 30
  }
  wait    = true
  restart = "unless-stopped"
}
resource "docker_container" "storage_init" {
  name    = "workforce-tofu-storage-init"
  image   = docker_image.alpine.image_id
  command = ["sh", "-c", "chown 1654:1654 /data/documents && chmod 700 /data/documents"]
  volumes {
    volume_name    = docker_volume.documents.name
    container_path = "/data/documents"
  }
  must_run = false
}
resource "docker_container" "ai" {
  name  = "workforce-tofu-ai"
  image = docker_image.application["ai"].image_id
  env   = ["INTERNAL_API_KEY=${var.internal_key}", "OLLAMA_URL=${var.ollama_url}", "OLLAMA_MODEL=deepseek-r1:1.5b", "EMBED_MODEL=nomic-embed-text"]
  networks_advanced {
    name    = docker_network.workforce.name
    aliases = ["ai"]
  }
  host {
    host = "host.docker.internal"
    ip   = "host-gateway"
  }
  healthcheck {
    test     = ["CMD", "python", "-c", "import urllib.request; urllib.request.urlopen('http://localhost:8000/health')"]
    interval = "5s"
    retries  = 30
  }
  wait    = true
  restart = "unless-stopped"
}
resource "docker_container" "api" {
  name       = "workforce-tofu-api"
  image      = docker_image.application["backend"].image_id
  depends_on = [docker_container.db, docker_container.ai, docker_container.storage_init]
  env        = ["ConnectionStrings__Database=Host=db;Database=workforce;Username=workforce;Password=${var.database_password}", "Jwt__Key=${var.jwt_key}", "Internal__Key=${var.internal_key}", "Intelligence__Url=http://ai:8000", "Storage__Path=/data/documents", "Seed__Enabled=false"]
  networks_advanced {
    name    = docker_network.workforce.name
    aliases = ["api"]
  }
  volumes {
    volume_name    = docker_volume.documents.name
    container_path = "/data/documents"
  }
  restart = "unless-stopped"
}
resource "docker_container" "web" {
  name       = "workforce-tofu-web"
  image      = docker_image.application["frontend"].image_id
  depends_on = [docker_container.api]
  networks_advanced {
    name = docker_network.workforce.name
  }
  ports {
    internal = 80
    external = var.web_port
    ip       = "127.0.0.1"
  }
  restart = "unless-stopped"
}
output "local_url" {
  value = "http://localhost:${var.web_port}"
}
