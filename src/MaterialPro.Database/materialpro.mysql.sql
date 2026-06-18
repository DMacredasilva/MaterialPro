CREATE DATABASE IF NOT EXISTS materialpro CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE materialpro;

CREATE USER IF NOT EXISTS 'materialpro_system'@'%' IDENTIFIED BY 'MaterialPro@123!';
GRANT ALL PRIVILEGES ON materialpro.* TO 'materialpro_system'@'%';
FLUSH PRIVILEGES;

CREATE TABLE IF NOT EXISTS `Customers` (
  `Id` char(36) NOT NULL,
  `CreatedAtUtc` datetime(6) NOT NULL,
  `UpdatedAtUtc` datetime(6) NULL,
  `IsActive` tinyint(1) NOT NULL DEFAULT 1,
  `Code` varchar(40) NOT NULL DEFAULT '',
  `FullName` varchar(150) NOT NULL,
  `DocumentNumber` varchar(30) NOT NULL DEFAULT '',
  `StateRegistration` varchar(30) NOT NULL DEFAULT '',
  `Phone` varchar(30) NOT NULL DEFAULT '',
  `WhatsApp` varchar(30) NOT NULL DEFAULT '',
  `Email` varchar(180) NOT NULL DEFAULT '',
  `ZipCode` varchar(20) NOT NULL DEFAULT '',
  `Address` varchar(220) NOT NULL DEFAULT '',
  `AddressNumber` varchar(20) NOT NULL DEFAULT '',
  `Complement` varchar(120) NOT NULL DEFAULT '',
  `District` varchar(120) NOT NULL DEFAULT '',
  `City` varchar(120) NOT NULL DEFAULT '',
  `State` varchar(2) NOT NULL DEFAULT '',
  `CreditLimit` decimal(18,2) NOT NULL DEFAULT 0,
  `Notes` varchar(500) NOT NULL DEFAULT '',
  `IsBlocked` tinyint(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

ALTER TABLE `Customers`
  ADD COLUMN IF NOT EXISTS `Code` varchar(40) NOT NULL DEFAULT '',
  ADD COLUMN IF NOT EXISTS `StateRegistration` varchar(30) NOT NULL DEFAULT '',
  ADD COLUMN IF NOT EXISTS `WhatsApp` varchar(30) NOT NULL DEFAULT '',
  ADD COLUMN IF NOT EXISTS `ZipCode` varchar(20) NOT NULL DEFAULT '',
  ADD COLUMN IF NOT EXISTS `Address` varchar(220) NOT NULL DEFAULT '',
  ADD COLUMN IF NOT EXISTS `AddressNumber` varchar(20) NOT NULL DEFAULT '',
  ADD COLUMN IF NOT EXISTS `Complement` varchar(120) NOT NULL DEFAULT '',
  ADD COLUMN IF NOT EXISTS `District` varchar(120) NOT NULL DEFAULT '',
  ADD COLUMN IF NOT EXISTS `City` varchar(120) NOT NULL DEFAULT '',
  ADD COLUMN IF NOT EXISTS `State` varchar(2) NOT NULL DEFAULT '',
  ADD COLUMN IF NOT EXISTS `CreditLimit` decimal(18,2) NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS `Notes` varchar(500) NOT NULL DEFAULT '',
  ADD COLUMN IF NOT EXISTS `IsBlocked` tinyint(1) NOT NULL DEFAULT 0;

SET @sql = IF((SELECT COUNT(*) FROM information_schema.statistics WHERE table_schema = DATABASE() AND table_name = 'Customers' AND index_name = 'IX_Customers_Code') = 0, 'CREATE INDEX IX_Customers_Code ON Customers (Code)', 'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
SET @sql = IF((SELECT COUNT(*) FROM information_schema.statistics WHERE table_schema = DATABASE() AND table_name = 'Customers' AND index_name = 'IX_Customers_FullName') = 0, 'CREATE INDEX IX_Customers_FullName ON Customers (FullName)', 'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
SET @sql = IF((SELECT COUNT(*) FROM information_schema.statistics WHERE table_schema = DATABASE() AND table_name = 'Customers' AND index_name = 'IX_Customers_DocumentNumber') = 0, 'CREATE INDEX IX_Customers_DocumentNumber ON Customers (DocumentNumber)', 'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
SET @sql = IF((SELECT COUNT(*) FROM information_schema.statistics WHERE table_schema = DATABASE() AND table_name = 'Customers' AND index_name = 'IX_Customers_Phone') = 0, 'CREATE INDEX IX_Customers_Phone ON Customers (Phone)', 'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
SET @sql = IF((SELECT COUNT(*) FROM information_schema.statistics WHERE table_schema = DATABASE() AND table_name = 'Customers' AND index_name = 'IX_Customers_WhatsApp') = 0, 'CREATE INDEX IX_Customers_WhatsApp ON Customers (WhatsApp)', 'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

CREATE TABLE IF NOT EXISTS `Suppliers` (
  `Id` char(36) NOT NULL,
  `CreatedAtUtc` datetime(6) NOT NULL,
  `UpdatedAtUtc` datetime(6) NULL,
  `IsActive` tinyint(1) NOT NULL DEFAULT 1,
  `Name` varchar(150) NOT NULL DEFAULT '',
  `Cnpj` varchar(30) NOT NULL DEFAULT '',
  `Phone` varchar(30) NOT NULL DEFAULT '',
  `Email` varchar(180) NOT NULL DEFAULT '',
  `Address` varchar(220) NOT NULL DEFAULT '',
  `Code` varchar(40) NOT NULL DEFAULT '',
  `PersonType` int NOT NULL DEFAULT 2,
  `FantasyName` varchar(150) NOT NULL DEFAULT '',
  `LegalName` varchar(180) NOT NULL DEFAULT '',
  `StateRegistration` varchar(30) NOT NULL DEFAULT '',
  `MunicipalRegistration` varchar(30) NOT NULL DEFAULT '',
  `MobilePhone` varchar(30) NOT NULL DEFAULT '',
  `WhatsApp` varchar(30) NOT NULL DEFAULT '',
  `Website` varchar(180) NOT NULL DEFAULT '',
  `ZipCode` varchar(20) NOT NULL DEFAULT '',
  `AddressNumber` varchar(20) NOT NULL DEFAULT '',
  `Complement` varchar(120) NOT NULL DEFAULT '',
  `District` varchar(120) NOT NULL DEFAULT '',
  `City` varchar(120) NOT NULL DEFAULT '',
  `State` varchar(2) NOT NULL DEFAULT '',
  `ContactName` varchar(150) NOT NULL DEFAULT '',
  `ContactRole` varchar(120) NOT NULL DEFAULT '',
  `DefaultPaymentTermDays` int NOT NULL DEFAULT 0,
  `PurchaseLimit` decimal(18,2) NOT NULL DEFAULT 0,
  `Notes` varchar(500) NOT NULL DEFAULT '',
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

ALTER TABLE `Suppliers`
  ADD COLUMN IF NOT EXISTS `Code` varchar(40) NOT NULL DEFAULT '',
  ADD COLUMN IF NOT EXISTS `PersonType` int NOT NULL DEFAULT 2,
  ADD COLUMN IF NOT EXISTS `FantasyName` varchar(150) NOT NULL DEFAULT '',
  ADD COLUMN IF NOT EXISTS `LegalName` varchar(180) NOT NULL DEFAULT '',
  ADD COLUMN IF NOT EXISTS `StateRegistration` varchar(30) NOT NULL DEFAULT '',
  ADD COLUMN IF NOT EXISTS `MunicipalRegistration` varchar(30) NOT NULL DEFAULT '',
  ADD COLUMN IF NOT EXISTS `MobilePhone` varchar(30) NOT NULL DEFAULT '',
  ADD COLUMN IF NOT EXISTS `WhatsApp` varchar(30) NOT NULL DEFAULT '',
  ADD COLUMN IF NOT EXISTS `Website` varchar(180) NOT NULL DEFAULT '',
  ADD COLUMN IF NOT EXISTS `ZipCode` varchar(20) NOT NULL DEFAULT '',
  ADD COLUMN IF NOT EXISTS `AddressNumber` varchar(20) NOT NULL DEFAULT '',
  ADD COLUMN IF NOT EXISTS `Complement` varchar(120) NOT NULL DEFAULT '',
  ADD COLUMN IF NOT EXISTS `District` varchar(120) NOT NULL DEFAULT '',
  ADD COLUMN IF NOT EXISTS `City` varchar(120) NOT NULL DEFAULT '',
  ADD COLUMN IF NOT EXISTS `State` varchar(2) NOT NULL DEFAULT '',
  ADD COLUMN IF NOT EXISTS `ContactName` varchar(150) NOT NULL DEFAULT '',
  ADD COLUMN IF NOT EXISTS `ContactRole` varchar(120) NOT NULL DEFAULT '',
  ADD COLUMN IF NOT EXISTS `DefaultPaymentTermDays` int NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS `PurchaseLimit` decimal(18,2) NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS `Notes` varchar(500) NOT NULL DEFAULT '';

ALTER TABLE `Products` ADD COLUMN IF NOT EXISTS `SupplierId` char(36) NULL;
ALTER TABLE `AccountsPayable` ADD COLUMN IF NOT EXISTS `SupplierId` char(36) NULL;

CREATE TABLE IF NOT EXISTS `Purchases` (
  `Id` char(36) NOT NULL,
  `CreatedAtUtc` datetime(6) NOT NULL,
  `UpdatedAtUtc` datetime(6) NULL,
  `IsActive` tinyint(1) NOT NULL DEFAULT 1,
  `SupplierId` char(36) NOT NULL,
  `Number` varchar(60) NOT NULL DEFAULT '',
  `PurchasedAtUtc` datetime(6) NOT NULL,
  `TotalAmount` decimal(18,2) NOT NULL DEFAULT 0,
  `Notes` varchar(500) NOT NULL DEFAULT '',
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `PurchaseItems` (
  `Id` char(36) NOT NULL,
  `CreatedAtUtc` datetime(6) NOT NULL,
  `UpdatedAtUtc` datetime(6) NULL,
  `IsActive` tinyint(1) NOT NULL DEFAULT 1,
  `PurchaseId` char(36) NOT NULL,
  `ProductId` char(36) NOT NULL,
  `Quantity` decimal(18,3) NOT NULL DEFAULT 0,
  `UnitCost` decimal(18,2) NOT NULL DEFAULT 0,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

SET @sql = IF((SELECT COUNT(*) FROM information_schema.statistics WHERE table_schema = DATABASE() AND table_name = 'Suppliers' AND index_name = 'IX_Suppliers_FantasyName') = 0, 'CREATE INDEX IX_Suppliers_FantasyName ON Suppliers (FantasyName)', 'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
SET @sql = IF((SELECT COUNT(*) FROM information_schema.statistics WHERE table_schema = DATABASE() AND table_name = 'Suppliers' AND index_name = 'IX_Suppliers_LegalName') = 0, 'CREATE INDEX IX_Suppliers_LegalName ON Suppliers (LegalName)', 'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
SET @sql = IF((SELECT COUNT(*) FROM information_schema.statistics WHERE table_schema = DATABASE() AND table_name = 'Suppliers' AND index_name = 'IX_Suppliers_Cnpj') = 0, 'CREATE INDEX IX_Suppliers_Cnpj ON Suppliers (Cnpj)', 'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
SET @sql = IF((SELECT COUNT(*) FROM information_schema.statistics WHERE table_schema = DATABASE() AND table_name = 'Suppliers' AND index_name = 'IX_Suppliers_Phone') = 0, 'CREATE INDEX IX_Suppliers_Phone ON Suppliers (Phone)', 'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
SET @sql = IF((SELECT COUNT(*) FROM information_schema.statistics WHERE table_schema = DATABASE() AND table_name = 'Suppliers' AND index_name = 'IX_Suppliers_WhatsApp') = 0, 'CREATE INDEX IX_Suppliers_WhatsApp ON Suppliers (WhatsApp)', 'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

CREATE TABLE IF NOT EXISTS `impressoras` (
  `id` char(36) NOT NULL,
  `CreatedAtUtc` datetime(6) NOT NULL,
  `UpdatedAtUtc` datetime(6) NULL,
  `ativa` tinyint(1) NOT NULL DEFAULT 1,
  `nome` varchar(180) NOT NULL DEFAULT '',
  `driver` varchar(180) NOT NULL DEFAULT '',
  `porta` varchar(120) NOT NULL DEFAULT '',
  `tipo` int NOT NULL DEFAULT 0,
  `status` int NOT NULL DEFAULT 1,
  `padrao_windows` tinyint(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  INDEX `IX_impressoras_nome` (`nome`),
  INDEX `IX_impressoras_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `configuracoes_impressao` (
  `id` char(36) NOT NULL,
  `CreatedAtUtc` datetime(6) NOT NULL,
  `UpdatedAtUtc` datetime(6) NULL,
  `IsActive` tinyint(1) NOT NULL DEFAULT 1,
  `computador` varchar(120) NOT NULL DEFAULT '',
  `tipo_documento` int NOT NULL,
  `impressora_id` char(36) NULL,
  `largura_papel` int NOT NULL DEFAULT 2,
  `margem_esquerda` decimal(18,2) NOT NULL DEFAULT 4,
  `margem_direita` decimal(18,2) NOT NULL DEFAULT 4,
  `margem_superior` decimal(18,2) NOT NULL DEFAULT 4,
  `margem_inferior` decimal(18,2) NOT NULL DEFAULT 4,
  `cortar_papel` tinyint(1) NOT NULL DEFAULT 1,
  `abrir_gaveta` tinyint(1) NOT NULL DEFAULT 0,
  `imprimir_logo` tinyint(1) NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`),
  INDEX `IX_configuracoes_impressao_tipo` (`tipo_documento`),
  INDEX `IX_configuracoes_impressao_computador` (`computador`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `filas_impressao` (
  `id` char(36) NOT NULL,
  `CreatedAtUtc` datetime(6) NOT NULL,
  `UpdatedAtUtc` datetime(6) NULL,
  `IsActive` tinyint(1) NOT NULL DEFAULT 1,
  `tipo_documento` int NOT NULL,
  `referencia_id` char(36) NULL,
  `impressora_id` char(36) NULL,
  `status` int NOT NULL DEFAULT 1,
  `tentativas` int NOT NULL DEFAULT 0,
  `conteudo` longtext NOT NULL,
  `erro` varchar(1000) NOT NULL DEFAULT '',
  `impresso_em` datetime(6) NULL,
  PRIMARY KEY (`id`),
  INDEX `IX_filas_impressao_status` (`status`),
  INDEX `IX_filas_impressao_documento` (`tipo_documento`),
  INDEX `IX_filas_impressao_referencia` (`referencia_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `logs_impressao` (
  `id` char(36) NOT NULL,
  `CreatedAtUtc` datetime(6) NOT NULL,
  `UpdatedAtUtc` datetime(6) NULL,
  `IsActive` tinyint(1) NOT NULL DEFAULT 1,
  `usuario_id` char(36) NULL,
  `tipo_documento` int NOT NULL,
  `referencia_id` char(36) NULL,
  `impressora` varchar(180) NOT NULL DEFAULT '',
  `status` int NOT NULL,
  `mensagem` varchar(1000) NOT NULL DEFAULT '',
  `data_log` datetime(6) NOT NULL,
  PRIMARY KEY (`id`),
  INDEX `IX_logs_impressao_data` (`data_log`),
  INDEX `IX_logs_impressao_documento` (`tipo_documento`),
  INDEX `IX_logs_impressao_usuario` (`usuario_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
