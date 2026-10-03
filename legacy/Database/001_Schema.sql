-- Comptoir Durand - gestion commerciale
-- Schema v1 (2014). Ne pas modifier sans prevenir l'equipe magasin.

CREATE TABLE dbo.Users (
    UserId        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
    Login         NVARCHAR(30)  NOT NULL CONSTRAINT UQ_Users_Login UNIQUE,
    DisplayName   NVARCHAR(80)  NOT NULL,
    PasswordHash  NVARCHAR(200) NOT NULL,
    Role          NVARCHAR(20)  NOT NULL -- 'commercial', 'magasin', 'admin'
);

CREATE TABLE dbo.Customers (
    CustomerId    INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Customers PRIMARY KEY,
    Code          NVARCHAR(10)  NOT NULL CONSTRAINT UQ_Customers_Code UNIQUE,
    Name          NVARCHAR(100) NOT NULL,
    City          NVARCHAR(60)  NULL,
    Tier          CHAR(1)       NOT NULL CONSTRAINT DF_Customers_Tier DEFAULT ('C'), -- A, B ou C (remise)
    IsActive      BIT           NOT NULL CONSTRAINT DF_Customers_IsActive DEFAULT (1)
);

CREATE TABLE dbo.Products (
    ProductId     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Products PRIMARY KEY,
    Sku           NVARCHAR(20)  NOT NULL CONSTRAINT UQ_Products_Sku UNIQUE,
    Label         NVARCHAR(120) NOT NULL,
    Category      NVARCHAR(40)  NOT NULL,
    UnitPrice     DECIMAL(10,2) NOT NULL, -- prix HT
    VatCode       CHAR(1)       NOT NULL, -- N = normal, I = intermediaire, R = reduit
    StockQty      INT           NOT NULL,
    ReservedQty   INT           NOT NULL CONSTRAINT DF_Products_ReservedQty DEFAULT (0),
    IsActive      BIT           NOT NULL CONSTRAINT DF_Products_IsActive DEFAULT (1)
);

CREATE TABLE dbo.Orders (
    OrderId       INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Orders PRIMARY KEY,
    OrderNumber   NVARCHAR(20)  NULL,
    CustomerId    INT           NOT NULL CONSTRAINT FK_Orders_Customers REFERENCES dbo.Customers (CustomerId),
    Status        TINYINT       NOT NULL CONSTRAINT DF_Orders_Status DEFAULT (0), -- 0 brouillon, 1 confirmee, 2 expediee, 3 facturee, 9 annulee
    CreatedOn     DATETIME      NOT NULL CONSTRAINT DF_Orders_CreatedOn DEFAULT (GETDATE()),
    CreatedBy     NVARCHAR(30)  NULL,
    ConfirmedOn   DATETIME      NULL,
    ShippedOn     DATETIME      NULL,
    Comment       NVARCHAR(500) NULL,
    TotalHT       DECIMAL(12,2) NULL,
    ShippingHT    DECIMAL(10,2) NULL,
    TotalVAT      DECIMAL(12,2) NULL,
    TotalTTC      DECIMAL(12,2) NULL
);
CREATE UNIQUE INDEX UX_Orders_OrderNumber ON dbo.Orders (OrderNumber) WHERE OrderNumber IS NOT NULL;
CREATE INDEX IX_Orders_CustomerId ON dbo.Orders (CustomerId);

CREATE TABLE dbo.OrderLines (
    OrderLineId   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OrderLines PRIMARY KEY,
    OrderId       INT           NOT NULL CONSTRAINT FK_OrderLines_Orders REFERENCES dbo.Orders (OrderId),
    ProductId     INT           NOT NULL CONSTRAINT FK_OrderLines_Products REFERENCES dbo.Products (ProductId),
    Quantity      INT           NOT NULL,
    UnitPrice     DECIMAL(10,2) NULL,
    DiscountPct   DECIMAL(5,2)  NULL,
    LineHT        DECIMAL(12,2) NULL,
    LineVAT       DECIMAL(12,2) NULL
);
CREATE INDEX IX_OrderLines_OrderId ON dbo.OrderLines (OrderId);

CREATE TABLE dbo.Invoices (
    InvoiceId     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Invoices PRIMARY KEY,
    InvoiceNumber NVARCHAR(20)  NOT NULL CONSTRAINT UQ_Invoices_Number UNIQUE,
    OrderId       INT           NOT NULL CONSTRAINT UQ_Invoices_Order UNIQUE CONSTRAINT FK_Invoices_Orders REFERENCES dbo.Orders (OrderId),
    IssuedOn      DATETIME      NOT NULL,
    TotalTTC      DECIMAL(12,2) NOT NULL
);

-- Compteurs annuels (numeros de commande et de facture). Factures : numerotation continue obligatoire.
CREATE TABLE dbo.Counters (
    Name          NVARCHAR(20)  NOT NULL,
    Year          INT           NOT NULL,
    LastValue     INT           NOT NULL,
    CONSTRAINT PK_Counters PRIMARY KEY (Name, Year)
);
