terraform {
  required_version = ">= 1.5"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.50"
    }
    # Writes the Function App's App Settings exactly, see function.tf (azurerm injects one we do not want).
    azapi = {
      source  = "Azure/azapi"
      version = "~> 2.0"
    }
  }
}

# The subscription comes from the ARM_SUBSCRIPTION_ID environment variable.
provider "azurerm" {
  features {}

  # Containers, tables and queues are managed through Entra ID, because the storage account keys are off
  # (shared_access_key_enabled in storage.tf). The Terraform identity holds the data roles (roles.tf).
  storage_use_azuread = true
}

# Signs in like azurerm: ARM_USE_OIDC, ARM_CLIENT_ID, ARM_TENANT_ID and ARM_SUBSCRIPTION_ID.
provider "azapi" {}
