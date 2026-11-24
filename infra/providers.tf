terraform {
  required_version = ">= 1.5"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
  }
}

# Підписка береться зі змінної середовища ARM_SUBSCRIPTION_ID.
provider "azurerm" {
  features {}
}
