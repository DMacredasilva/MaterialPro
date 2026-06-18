CREATE TABLE IF NOT EXISTS `Products` (
  `Id` char(36) NOT NULL,
  `CreatedAtUtc` datetime(6) NOT NULL,
  `UpdatedAtUtc` datetime(6) NULL,
  `IsActive` tinyint(1) NOT NULL DEFAULT 1,
  `Sku` varchar(60) NOT NULL,
  `Name` varchar(180) NOT NULL,
  `Description` varchar(500) NOT NULL DEFAULT '',
  `Category` varchar(120) NOT NULL DEFAULT '',
  `Brand` varchar(120) NOT NULL DEFAULT '',
  `Unit` varchar(20) NOT NULL DEFAULT 'UN',
  `SalePrice` decimal(18,2) NOT NULL DEFAULT 0,
  `CostPrice` decimal(18,2) NOT NULL DEFAULT 0,
  `StockQuantity` decimal(18,3) NOT NULL DEFAULT 0,
  `MinimumStock` decimal(18,3) NOT NULL DEFAULT 0,
  `Barcode` varchar(80) NOT NULL DEFAULT '',
  `Ncm` varchar(20) NOT NULL DEFAULT '',
  `Location` varchar(120) NOT NULL DEFAULT '',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_Products_Sku` (`Sku`),
  KEY `IX_Products_Barcode` (`Barcode`),
  KEY `IX_Products_Category` (`Category`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `StockMovements` (
  `Id` char(36) NOT NULL,
  `CreatedAtUtc` datetime(6) NOT NULL,
  `UpdatedAtUtc` datetime(6) NULL,
  `IsActive` tinyint(1) NOT NULL DEFAULT 1,
  `ProductId` char(36) NOT NULL,
  `Quantity` decimal(18,3) NOT NULL,
  `Reason` longtext NOT NULL,
  `Reference` longtext NOT NULL,
  `MovementAtUtc` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_StockMovements_ProductId` (`ProductId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

ALTER TABLE `Products`
  ADD COLUMN IF NOT EXISTS `Description` varchar(500) NOT NULL DEFAULT '',
  ADD COLUMN IF NOT EXISTS `Category` varchar(120) NOT NULL DEFAULT '',
  ADD COLUMN IF NOT EXISTS `Brand` varchar(120) NOT NULL DEFAULT '',
  ADD COLUMN IF NOT EXISTS `Ncm` varchar(20) NOT NULL DEFAULT '',
  ADD COLUMN IF NOT EXISTS `Location` varchar(120) NOT NULL DEFAULT '';
