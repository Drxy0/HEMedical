data "azurerm_client_config" "current" {}

resource "azurerm_resource_group" "rg" {
  name     = "${var.prefix}-rg"
  location = var.location
}

# --- Container registry -----------------------------------------------------

resource "azurerm_container_registry" "acr" {
  name                = "${var.prefix}acr"
  resource_group_name = azurerm_resource_group.rg.name
  location            = azurerm_resource_group.rg.location
  sku                 = "Basic"
  admin_enabled       = false
}

# --- AKS cluster ------------------------------------------------------------

resource "azurerm_kubernetes_cluster" "aks" {
  name                = "${var.prefix}-aks"
  resource_group_name = azurerm_resource_group.rg.name
  location            = azurerm_resource_group.rg.location
  dns_prefix          = "${var.prefix}-aks"

  default_node_pool {
    name       = "default"
    node_count = var.node_count
    vm_size    = var.node_vm_size
  }

  identity {
    type = "SystemAssigned"
  }

  # Enables the Key Vault Secrets Store CSI driver, so pods can later mount
  # secrets directly from Key Vault if you move off Kubernetes Secrets.
  key_vault_secrets_provider {
    secret_rotation_enabled = true
  }
}

# Let the cluster pull images from ACR without registry credentials.
resource "azurerm_role_assignment" "aks_acr_pull" {
  scope                            = azurerm_container_registry.acr.id
  role_definition_name             = "AcrPull"
  principal_id                     = azurerm_kubernetes_cluster.aks.kubelet_identity[0].object_id
  skip_service_principal_aad_check = true
}

# --- Azure SQL (serverless, auto-pausing) -----------------------------------

resource "azurerm_mssql_server" "sql" {
  name                         = "${var.prefix}-sql"
  resource_group_name          = azurerm_resource_group.rg.name
  location                     = azurerm_resource_group.rg.location
  version                      = "12.0"
  administrator_login          = var.sql_admin_login
  administrator_login_password = var.sql_admin_password
}

resource "azurerm_mssql_database" "db" {
  name                        = "HospitalDb"
  server_id                   = azurerm_mssql_server.sql.id
  sku_name                    = "GP_S_Gen5_1" # serverless, 1 vCore
  min_capacity                = 0.5
  auto_pause_delay_in_minutes = 60
  max_size_gb                 = 2
}

# Allow other Azure services (the AKS nodes) to reach the SQL server.
resource "azurerm_mssql_firewall_rule" "allow_azure" {
  name             = "AllowAzureServices"
  server_id        = azurerm_mssql_server.sql.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

# --- Key Vault (source of truth for secrets) --------------------------------

resource "azurerm_key_vault" "kv" {
  name                = "${var.prefix}-kv"
  resource_group_name = azurerm_resource_group.rg.name
  location            = azurerm_resource_group.rg.location
  tenant_id           = data.azurerm_client_config.current.tenant_id
  sku_name            = "standard"

  # Deployer can manage secrets.
  access_policy {
    tenant_id          = data.azurerm_client_config.current.tenant_id
    object_id          = data.azurerm_client_config.current.object_id
    secret_permissions = ["Get", "List", "Set", "Delete", "Purge", "Recover"]
  }

  # The AKS Key Vault CSI identity can read secrets (for the optional CSI upgrade path).
  access_policy {
    tenant_id          = data.azurerm_client_config.current.tenant_id
    object_id          = azurerm_kubernetes_cluster.aks.key_vault_secrets_provider[0].secret_identity[0].object_id
    secret_permissions = ["Get", "List"]
  }
}

locals {
  sql_connection_string = "Server=tcp:${azurerm_mssql_server.sql.fully_qualified_domain_name},1433;Database=${azurerm_mssql_database.db.name};User ID=${var.sql_admin_login};Password=${var.sql_admin_password};Encrypt=True;TrustServerCertificate=False;"
}

resource "azurerm_key_vault_secret" "sql_conn" {
  name         = "sql-connection-string"
  value        = local.sql_connection_string
  key_vault_id = azurerm_key_vault.kv.id
}

resource "azurerm_key_vault_secret" "loinc_username" {
  name         = "loinc-username"
  value        = var.loinc_username
  key_vault_id = azurerm_key_vault.kv.id
}

resource "azurerm_key_vault_secret" "loinc_password" {
  name         = "loinc-password"
  value        = var.loinc_password
  key_vault_id = azurerm_key_vault.kv.id
}

# --- Kubernetes namespace + secrets -----------------------------------------

resource "kubernetes_namespace" "app" {
  metadata {
    name = var.kubernetes_namespace
  }
}

# Database connection string, consumed by the Hospital service.
resource "kubernetes_secret" "db" {
  metadata {
    name      = "db-secret"
    namespace = kubernetes_namespace.app.metadata[0].name
  }
  data = {
    connectionString = local.sql_connection_string
  }
}

# LOINC terminology-server credentials, consumed by the Client.
resource "kubernetes_secret" "loinc" {
  metadata {
    name      = "loinc-secret"
    namespace = kubernetes_namespace.app.metadata[0].name
  }
  data = {
    username = var.loinc_username
    password = var.loinc_password
  }
}

# CKKS key pair. The Client mounts both; the Proxy mounts only the public key.
# Loaded from the committed key files so every replica shares one consistent pair
# (regenerating per-pod would make the Proxy's public key mismatch the Client's secret key).
resource "kubernetes_secret" "he_keys" {
  metadata {
    name      = "he-keys"
    namespace = kubernetes_namespace.app.metadata[0].name
  }
  data = {
    "public.key" = filebase64(var.he_public_key_path)
    "secret.key" = filebase64(var.he_secret_key_path)
  }
}

# --- Ingress controller -----------------------------------------------------

resource "helm_release" "ingress_nginx" {
  name             = "ingress-nginx"
  repository       = "https://kubernetes.github.io/ingress-nginx"
  chart            = "ingress-nginx"
  namespace        = "ingress-nginx"
  create_namespace = true
  version          = "4.11.3"

  set {
    name  = "controller.service.type"
    value = "LoadBalancer"
  }
}
