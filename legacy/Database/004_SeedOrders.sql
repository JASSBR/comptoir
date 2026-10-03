-- Historique de demonstration : une commande a chaque etape du cycle.
SET NOCOUNT ON;
DECLARE @o INT;

INSERT INTO dbo.Orders (CustomerId, CreatedBy, Comment) SELECT CustomerId, N'sophie', N'Livraison avant 7 h' FROM dbo.Customers WHERE Code = N'BLG001';
SET @o = SCOPE_IDENTITY();
INSERT INTO dbo.OrderLines (OrderId, ProductId, Quantity) SELECT @o, ProductId, 12 FROM dbo.Products WHERE Sku = N'FAR-T65-25';
INSERT INTO dbo.OrderLines (OrderId, ProductId, Quantity) SELECT @o, ProductId, 2 FROM dbo.Products WHERE Sku = N'BEU-AOP-10';
INSERT INTO dbo.OrderLines (OrderId, ProductId, Quantity) SELECT @o, ProductId, 3 FROM dbo.Products WHERE Sku = N'EMB-SAC-1000';
EXEC dbo.usp_ConfirmOrder @o; EXEC dbo.usp_ShipOrder @o; EXEC dbo.usp_InvoiceOrder @o;

INSERT INTO dbo.Orders (CustomerId, CreatedBy) SELECT CustomerId, N'sophie' FROM dbo.Customers WHERE Code = N'PAT003';
SET @o = SCOPE_IDENTITY();
INSERT INTO dbo.OrderLines (OrderId, ProductId, Quantity) SELECT @o, ProductId, 4 FROM dbo.Products WHERE Sku = N'CHO-NOI-5';
INSERT INTO dbo.OrderLines (OrderId, ProductId, Quantity) SELECT @o, ProductId, 6 FROM dbo.Products WHERE Sku = N'AMA-POU-1';
INSERT INTO dbo.OrderLines (OrderId, ProductId, Quantity) SELECT @o, ProductId, 100 FROM dbo.Products WHERE Sku = N'EMB-BOI-100';
EXEC dbo.usp_ConfirmOrder @o; EXEC dbo.usp_ShipOrder @o;

INSERT INTO dbo.Orders (CustomerId, CreatedBy) SELECT CustomerId, N'sophie' FROM dbo.Customers WHERE Code = N'RES004';
SET @o = SCOPE_IDENTITY();
INSERT INTO dbo.OrderLines (OrderId, ProductId, Quantity) SELECT @o, ProductId, 5 FROM dbo.Products WHERE Sku = N'CAF-GRA-1';
INSERT INTO dbo.OrderLines (OrderId, ProductId, Quantity) SELECT @o, ProductId, 2 FROM dbo.Products WHERE Sku = N'BOI-JUS-6';
EXEC dbo.usp_ConfirmOrder @o;

INSERT INTO dbo.Orders (CustomerId, CreatedBy) SELECT CustomerId, N'sophie' FROM dbo.Customers WHERE Code = N'BLG002';
SET @o = SCOPE_IDENTITY();
INSERT INTO dbo.OrderLines (OrderId, ProductId, Quantity) SELECT @o, ProductId, 20 FROM dbo.Products WHERE Sku = N'FAR-T55-25';
INSERT INTO dbo.OrderLines (OrderId, ProductId, Quantity) SELECT @o, ProductId, 8 FROM dbo.Products WHERE Sku = N'LEV-FRA-1';
EXEC dbo.usp_PriceOrder @o;
