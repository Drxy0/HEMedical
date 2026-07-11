variable "prefix" {
  description = "Short name prefix for all resources (lowercase letters/numbers)."
  type        = string
  default     = "hemedical"
}

variable "location" {
  description = "Azure region."
  type        = string
  default     = "westeurope"
}

variable "node_count" {
  description = "Number of nodes in the AKS default pool."
  type        = number
  default     = 2
}

variable "node_vm_size" {
  description = "VM size for AKS nodes. Standard_B2s is cheap; SEAL is CPU-bound so bump for real load."
  type        = string
  default     = "Standard_B2s"
}

variable "kubernetes_namespace" {
  description = "Namespace the application is deployed into."
  type        = string
  default     = "hemedical"
}

variable "sql_admin_login" {
  description = "Azure SQL administrator login."
  type        = string
  default     = "hemedicaladmin"
}

variable "sql_admin_password" {
  description = "Azure SQL administrator password (required, no default). Set via TF_VAR_sql_admin_password."
  type        = string
  sensitive   = true
}

variable "loinc_username" {
  description = "fhir.loinc.org account username used for LOINC code verification."
  type        = string
  sensitive   = true
  default     = ""
}

variable "loinc_password" {
  description = "fhir.loinc.org account password."
  type        = string
  sensitive   = true
  default     = ""
}

variable "he_public_key_path" {
  description = "Path to the CKKS public key file to load into the cluster."
  type        = string
  default     = "../../HEMedical/HEMedical.Client/public.key"
}

variable "he_secret_key_path" {
  description = "Path to the CKKS secret key file to load into the cluster (Client only)."
  type        = string
  default     = "../../HEMedical/HEMedical.Client/secret.key"
}
