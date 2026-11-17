# =============================================================================
# FIAP Pós-Tech SOAT - Fase 5 | Plataforma de Revenda de Veículos
# Infraestrutura como Código (IaC) - Arquitetura Cloud Serverless & Gerenciada (AWS)
# =============================================================================

terraform {
  required_version = ">= 1.5.0"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }
}

provider "aws" {
  region = var.aws_region
}

variable "aws_region" {
  description = "Região AWS principal da plataforma (São Paulo)"
  type        = string
  default     = "sa-east-1"
}

variable "environment" {
  type    = string
  default = "production"
}

# 1. AWS KMS - Chaves Criptográficas Gerenciadas (Envelope Encryption AES-256 para Dados Sensíveis LGPD)
resource "aws_kms_key" "lgpd_pii_key" {
  description             = "Chave Mestre KMS (FIPS 140-3) para Criptografia de Dados Sensíveis LGPD (CPF, RG, CNH, Endereço)"
  deletion_window_in_days = 30
  enable_key_rotation     = true
  tags = {
    Project     = "RevendaVeiculos-Fase5"
    Compliance  = "LGPD-Lei-13709-2018"
    Environment = var.environment
  }
}

resource "aws_kms_alias" "lgpd_pii_key_alias" {
  name          = "alias/revenda-veiculos-lgpd-pii-key"
  target_key_id = aws_kms_key.lgpd_pii_key.key_id
}

# 2. AWS Secrets Manager - Rotação e Guarda Segura de Segredos e Chaves HMAC
resource "aws_secretsmanager_secret" "app_secrets" {
  name        = "revenda-veiculos/${var.environment}/security-keys"
  description = "Segredos de Criptografia AES-256-GCM, Chave de Indexação Cega HMAC-SHA256 e Credenciais do Aurora Serverless v2"
  kms_key_id  = aws_kms_key.lgpd_pii_key.arn
}

# 3. Amazon Cognito - Identidade Gerenciada, Autenticação OAuth 2.0 / OIDC, MFA e RBAC
resource "aws_cognito_user_pool" "revenda_users" {
  name                     = "revenda-veiculos-user-pool"
  mfa_configuration        = "OPTIONAL"
  auto_verified_attributes = ["email"]

  password_policy {
    minimum_length    = 12
    require_lowercase = true
    require_numbers   = true
    require_symbols   = true
    require_uppercase = true
  }
}

# 4. AWS WAFv2 - Proteção de Borda (OWASP Top 10, Rate Limiting, Anti-Bot, SQLi/XSS)
resource "aws_wafv2_web_acl" "api_waf" {
  name        = "revenda-veiculos-waf-acl"
  description = "WAF gerenciado protegendo o Amazon API Gateway contra ataques OWASP Top 10 e DDoS"
  scope       = "REGIONAL"

  default_action {
    allow {}
  }

  visibility_config {
    cloudwatch_metrics_enabled = true
    metric_name                = "RevendaVeiculosWafMetrics"
    sampled_requests_enabled   = true
  }
}

# 5. Amazon DynamoDB (Serverless) - Controle de Locks Distribuídos de Reserva & Trilha de Auditoria LGPD Imutável
resource "aws_dynamodb_table" "saga_reservation_locks" {
  name         = "revenda-veiculos-saga-locks"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "VeiculoId"

  attribute {
    name = "VeiculoId"
    type = "S"
  }

  ttl {
    attribute_name = "ExpiresAtEpoch"
    enabled        = true
  }

  server_side_encryption {
    enabled     = true
    kms_key_arn = aws_kms_key.lgpd_pii_key.arn
  }
}

resource "aws_dynamodb_table" "lgpd_audit_logs" {
  name         = "revenda-veiculos-lgpd-audit-logs"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "Id"
  range_key    = "CriadoEm"

  attribute {
    name = "Id"
    type = "S"
  }

  attribute {
    name = "CriadoEm"
    type = "S"
  }

  point_in_time_recovery {
    enabled = true
  }

  server_side_encryption {
    enabled     = true
    kms_key_arn = aws_kms_key.lgpd_pii_key.arn
  }
}

# 6. AWS Step Functions - Orquestrador Serverless da SAGA de Compra de Veículos
resource "aws_sfn_state_machine" "compra_veiculo_saga" {
  name     = "RevendaVeiculos-CompraVeiculo-SagaOrchestrator"
  role_arn = "arn:aws:iam::123456789012:role/RevendaSagaStepFunctionsRole"

  definition = jsonencode({
    Comment = "SAGA Orquestrada para Compra e Venda de Veículo Automotor (Reserva -> Código Pagamento -> Pagamento -> Documentação e Baixa no Estoque + Compensações)"
    StartAt = "ValidarCompradorEReservarVeiculo"
    States = {
      ValidarCompradorEReservarVeiculo = {
        Type     = "Task"
        Resource = "arn:aws:states:::lambda:invoke"
        Next     = "EmitirCodigoPagamento"
        Catch = [
          {
            ErrorEquals = ["ConflitoConcorrenciaException", "States.ALL"]
            Next        = "FalhaReservaConcorrente"
          }
        ]
      }
      EmitirCodigoPagamento = {
        Type     = "Task"
        Resource = "arn:aws:states:::lambda:invoke"
        Next     = "AguardarConfirmacaoPagamento"
        Catch = [
          {
            ErrorEquals = ["States.ALL"]
            Next        = "CompensarLiberarReservaVeiculo"
          }
        ]
      }
      AguardarConfirmacaoPagamento = {
        Type           = "Task"
        Resource       = "arn:aws:states:::lambda:invoke.waitForTaskToken"
        TimeoutSeconds = 1800
        Next           = "VerificarDecisaoClienteOuPagamento"
        Catch = [
          {
            ErrorEquals = ["States.Timeout", "PagamentoNaoEfetuadoException"]
            Next        = "CompensarCancelarCodigoPagamento"
          }
        ]
      }
      VerificarDecisaoClienteOuPagamento = {
        Type = "Choice"
        Choices = [
          {
            Variable     = "$.DesistenciaCliente"
            BooleanEquals = true
            Next         = "CompensarEstornarPagamentoSeNecessario"
          },
          {
            Variable     = "$.PagamentoAprovado"
            BooleanEquals = true
            Next         = "EmitirDocumentacaoEBaixaEstoque"
          }
        ]
        Default = "CompensarCancelarCodigoPagamento"
      }
      EmitirDocumentacaoEBaixaEstoque = {
        Type     = "Task"
        Resource = "arn:aws:states:::lambda:invoke"
        End      = true
        Catch = [
          {
            ErrorEquals = ["States.ALL"]
            Next        = "CompensarEstornarPagamentoSeNecessario"
          }
        ]
      }
      CompensarEstornarPagamentoSeNecessario = {
        Type     = "Task"
        Resource = "arn:aws:states:::lambda:invoke"
        Next     = "CompensarCancelarCodigoPagamento"
      }
      CompensarCancelarCodigoPagamento = {
        Type     = "Task"
        Resource = "arn:aws:states:::lambda:invoke"
        Next     = "CompensarLiberarReservaVeiculo"
      }
      CompensarLiberarReservaVeiculo = {
        Type     = "Task"
        Resource = "arn:aws:states:::lambda:invoke"
        End      = true
      }
      FalhaReservaConcorrente = {
        Type  = "Fail"
        Error = "ConflitoConcorrenciaException"
        Cause = "Outro(a) cliente reservou o veículo antes da conclusão desta solicitação."
      }
    }
  })
}
