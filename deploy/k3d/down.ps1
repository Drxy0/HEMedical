<#
.SYNOPSIS
  Tear down the local HEMedical k3d cluster.
.PARAMETER KeepCluster
  Only delete the app (namespace) but leave the k3d cluster + ingress-nginx running,
  so a follow-up up.ps1 is faster.
#>
[CmdletBinding()]
param(
  [switch]$KeepCluster
)

$ErrorActionPreference = 'Stop'
$Cluster = 'hemedical'

if ($KeepCluster) {
  Write-Host "==> Deleting the 'hemedical' namespace (keeping cluster + ingress-nginx)" -ForegroundColor Cyan
  kubectl delete namespace hemedical --ignore-not-found
} else {
  Write-Host "==> Deleting k3d cluster '$Cluster'" -ForegroundColor Cyan
  k3d cluster delete $Cluster
}
