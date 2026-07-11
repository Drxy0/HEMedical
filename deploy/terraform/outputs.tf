output "resource_group" {
  value = azurerm_resource_group.rg.name
}

output "acr_login_server" {
  description = "Container registry host, e.g. hemedicalacr.azurecr.io."
  value       = azurerm_container_registry.acr.login_server
}

output "acr_name" {
  value = azurerm_container_registry.acr.name
}

output "aks_cluster_name" {
  value = azurerm_kubernetes_cluster.aks.name
}

output "kubernetes_namespace" {
  value = kubernetes_namespace.app.metadata[0].name
}

output "sql_server_fqdn" {
  value = azurerm_mssql_server.sql.fully_qualified_domain_name
}

output "key_vault_name" {
  value = azurerm_key_vault.kv.name
}

output "get_credentials_command" {
  description = "Run this to point kubectl at the new cluster."
  value       = "az aks get-credentials --resource-group ${azurerm_resource_group.rg.name} --name ${azurerm_kubernetes_cluster.aks.name}"
}

output "ingress_ip_command" {
  description = "Run this after deploying to read the public ingress IP."
  value       = "kubectl get svc -n ingress-nginx ingress-nginx-controller -o jsonpath='{.status.loadBalancer.ingress[0].ip}'"
}
