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
  PRIMARY KEY (`Id`),
  KEY `IX_Customers_Code` (`Code`),
  KEY `IX_Customers_FullName` (`FullName`),
  KEY `IX_Customers_DocumentNumber` (`DocumentNumber`),
  KEY `IX_Customers_Phone` (`Phone`),
  KEY `IX_Customers_WhatsApp` (`WhatsApp`)
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
