-- Regles de gestion (valide par la direction commerciale, mars 2014)
--  * remise client : A = 10 %, B = 5 %, C = 0
--  * +3 % a partir de 100 unites sur une ligne
--  * remise plafonnee a 12 %
--  * franco de port a 300 EUR HT, sinon 15 EUR HT de frais (TVA 20 %)
--  * TVA calculee ligne par ligne

CREATE PROCEDURE dbo.usp_NextCounter
    @Name NVARCHAR(20),
    @Year INT,
    @Value INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Counters WITH (UPDLOCK, HOLDLOCK)
       SET @Value = LastValue = LastValue + 1
     WHERE Name = @Name AND Year = @Year;
    IF @@ROWCOUNT = 0
    BEGIN
        INSERT INTO dbo.Counters (Name, Year, LastValue) VALUES (@Name, @Year, 1);
        SET @Value = 1;
    END
END
GO

CREATE PROCEDURE dbo.usp_PriceOrder
    @OrderId INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Tier CHAR(1);
    SELECT @Tier = c.Tier FROM dbo.Orders o JOIN dbo.Customers c ON c.CustomerId = o.CustomerId WHERE o.OrderId = @OrderId;

    UPDATE l
       SET l.UnitPrice   = p.UnitPrice,
           l.DiscountPct = CASE WHEN d.Pct > 12 THEN 12 ELSE d.Pct END
      FROM dbo.OrderLines l
      JOIN dbo.Products p ON p.ProductId = l.ProductId
     CROSS APPLY (SELECT CASE @Tier WHEN 'A' THEN 10 WHEN 'B' THEN 5 ELSE 0 END
                         + CASE WHEN l.Quantity >= 100 THEN 3 ELSE 0 END AS Pct) d
     WHERE l.OrderId = @OrderId;

    UPDATE l
       SET l.LineHT = ROUND(l.Quantity * l.UnitPrice * (100 - l.DiscountPct) / 100, 2)
      FROM dbo.OrderLines l
     WHERE l.OrderId = @OrderId;

    UPDATE l
       SET l.LineVAT = ROUND(l.LineHT * CASE p.VatCode WHEN 'N' THEN 0.20 WHEN 'I' THEN 0.10 WHEN 'R' THEN 0.055 ELSE 0 END, 2)
      FROM dbo.OrderLines l
      JOIN dbo.Products p ON p.ProductId = l.ProductId
     WHERE l.OrderId = @OrderId;

    DECLARE @TotalHT DECIMAL(12,2), @LinesVAT DECIMAL(12,2), @Shipping DECIMAL(10,2);
    SELECT @TotalHT = ISNULL(SUM(LineHT), 0), @LinesVAT = ISNULL(SUM(LineVAT), 0) FROM dbo.OrderLines WHERE OrderId = @OrderId;
    SET @Shipping = CASE WHEN @TotalHT < 300 THEN 15 ELSE 0 END;

    UPDATE dbo.Orders
       SET TotalHT    = @TotalHT,
           ShippingHT = @Shipping,
           TotalVAT   = @LinesVAT + ROUND(@Shipping * 0.20, 2),
           TotalTTC   = @TotalHT + @Shipping + @LinesVAT + ROUND(@Shipping * 0.20, 2)
     WHERE OrderId = @OrderId;
END
GO

CREATE PROCEDURE dbo.usp_ConfirmOrder
    @OrderId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRAN;

        IF NOT EXISTS (SELECT 1 FROM dbo.Orders WITH (UPDLOCK) WHERE OrderId = @OrderId AND Status = 0)
            RAISERROR (N'Commande introuvable ou deja confirmee.', 16, 1, 50002);
        IF NOT EXISTS (SELECT 1 FROM dbo.OrderLines WHERE OrderId = @OrderId)
            RAISERROR (N'Commande vide.', 16, 1, 50003);

        EXEC dbo.usp_PriceOrder @OrderId;

        IF EXISTS (
            SELECT 1
              FROM (SELECT ProductId, SUM(Quantity) AS Qty FROM dbo.OrderLines WHERE OrderId = @OrderId GROUP BY ProductId) q
              JOIN dbo.Products p WITH (UPDLOCK) ON p.ProductId = q.ProductId
             WHERE p.StockQty - p.ReservedQty < q.Qty)
            RAISERROR (N'Stock insuffisant.', 16, 1, 50001);

        UPDATE p
           SET p.ReservedQty = p.ReservedQty + q.Qty
          FROM dbo.Products p
          JOIN (SELECT ProductId, SUM(Quantity) AS Qty FROM dbo.OrderLines WHERE OrderId = @OrderId GROUP BY ProductId) q
            ON q.ProductId = p.ProductId;

        DECLARE @Year INT = YEAR(GETDATE()), @Value INT;
        EXEC dbo.usp_NextCounter N'CMD', @Year, @Value OUTPUT;

        UPDATE dbo.Orders
           SET Status = 1,
               ConfirmedOn = GETDATE(),
               OrderNumber = N'CMD-' + CAST(@Year AS NVARCHAR(4)) + N'-' + RIGHT(N'00000' + CAST(@Value AS NVARCHAR(10)), 5)
         WHERE OrderId = @OrderId;

        COMMIT;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END
GO

CREATE PROCEDURE dbo.usp_ShipOrder
    @OrderId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRAN;
    IF NOT EXISTS (SELECT 1 FROM dbo.Orders WITH (UPDLOCK) WHERE OrderId = @OrderId AND Status = 1)
    BEGIN
        ROLLBACK;
        RAISERROR (N'Seule une commande confirmee peut etre expediee.', 16, 1);
        RETURN;
    END
    UPDATE p
       SET p.StockQty = p.StockQty - q.Qty,
           p.ReservedQty = p.ReservedQty - q.Qty
      FROM dbo.Products p
      JOIN (SELECT ProductId, SUM(Quantity) AS Qty FROM dbo.OrderLines WHERE OrderId = @OrderId GROUP BY ProductId) q
        ON q.ProductId = p.ProductId;
    UPDATE dbo.Orders SET Status = 2, ShippedOn = GETDATE() WHERE OrderId = @OrderId;
    COMMIT;
END
GO

CREATE PROCEDURE dbo.usp_InvoiceOrder
    @OrderId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRAN;
    IF NOT EXISTS (SELECT 1 FROM dbo.Orders WITH (UPDLOCK) WHERE OrderId = @OrderId AND Status = 2)
    BEGIN
        ROLLBACK;
        RAISERROR (N'Seule une commande expediee peut etre facturee.', 16, 1);
        RETURN;
    END
    DECLARE @Year INT = YEAR(GETDATE()), @Value INT;
    EXEC dbo.usp_NextCounter N'FA', @Year, @Value OUTPUT;
    INSERT INTO dbo.Invoices (InvoiceNumber, OrderId, IssuedOn, TotalTTC)
    SELECT N'FA-' + CAST(@Year AS NVARCHAR(4)) + N'-' + RIGHT(N'00000' + CAST(@Value AS NVARCHAR(10)), 5), OrderId, GETDATE(), TotalTTC
      FROM dbo.Orders WHERE OrderId = @OrderId;
    UPDATE dbo.Orders SET Status = 3 WHERE OrderId = @OrderId;
    COMMIT;
END
GO

CREATE PROCEDURE dbo.usp_CancelOrder
    @OrderId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRAN;
    DECLARE @Status TINYINT;
    SELECT @Status = Status FROM dbo.Orders WITH (UPDLOCK) WHERE OrderId = @OrderId;
    IF @Status IS NULL OR @Status NOT IN (0, 1)
    BEGIN
        ROLLBACK;
        RAISERROR (N'Commande non annulable.', 16, 1);
        RETURN;
    END
    IF @Status = 1
        UPDATE p
           SET p.ReservedQty = p.ReservedQty - q.Qty
          FROM dbo.Products p
          JOIN (SELECT ProductId, SUM(Quantity) AS Qty FROM dbo.OrderLines WHERE OrderId = @OrderId GROUP BY ProductId) q
            ON q.ProductId = p.ProductId;
    UPDATE dbo.Orders SET Status = 9 WHERE OrderId = @OrderId;
    COMMIT;
END
GO
