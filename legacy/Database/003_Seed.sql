-- Donnees de demonstration. Mot de passe de tous les comptes : comptoir-demo
SET NOCOUNT ON;
INSERT INTO dbo.Users (Login, DisplayName, PasswordHash, Role) VALUES
    (N'sophie', N'Sophie Moreau', N'AFrXjrbuPCGaVzROnAVXiii2hXtNY1K3ue9jyWxyvKhX/1+n9fb2TybjDRh1PpxXIg==', N'commercial'),
    (N'marc', N'Marc Lefebvre', N'APMH+wNorSjQOGH3+qoeXJaHK4KhlEVo7QdP37YMPZdapBelt5neeteTIhabo+TbJg==', N'magasin'),
    (N'claire', N'Claire Durand', N'AEQaHEICB4u8b5FXB6QhFXc97VRTpSIDOcyWjwbt6wEgDi97A/PO6euX5wXPrF1UYg==', N'admin');
INSERT INTO dbo.Customers (Code, Name, City, Tier) VALUES
    (N'BLG001', N'Boulangerie Martin', N'Lyon', 'A'),
    (N'BLG002', N'Au Fournil de Paul', N'Villeurbanne', 'B'),
    (N'PAT003', N'Patisserie Lenoir', N'Lyon', 'A'),
    (N'RES004', N'Restaurant Le Bouchon', N'Lyon', 'C'),
    (N'BLG005', N'La Mie Doree', N'Bron', 'B'),
    (N'EPI006', N'Epicerie du Marche', N'Venissieux', 'C');
INSERT INTO dbo.Products (Sku, Label, Category, UnitPrice, VatCode, StockQty) VALUES
    (N'FAR-T55-25', N'Farine de ble T55 - sac 25 kg', N'Farines', 17.90, 'R', 400),
    (N'FAR-T65-25', N'Farine de ble T65 tradition - sac 25 kg', N'Farines', 19.40, 'R', 250),
    (N'FAR-SEI-10', N'Farine de seigle T130 - sac 10 kg', N'Farines', 12.75, 'R', 120),
    (N'SUC-SEM-25', N'Sucre semoule - sac 25 kg', N'Sucres', 24.60, 'R', 180),
    (N'SUC-GLA-10', N'Sucre glace - sac 10 kg', N'Sucres', 14.35, 'R', 90),
    (N'BEU-AOP-10', N'Beurre AOP Charentes-Poitou - plaque 10 kg', N'Cremerie', 98.00, 'R', 60),
    (N'BEU-TOU-2', N'Beurre de tourage 84 % - plaque 2 kg', N'Cremerie', 21.90, 'R', 150),
    (N'LEV-FRA-1', N'Levure fraiche - pain 1 kg', N'Ingredients', 3.85, 'R', 300),
    (N'SEL-FIN-25', N'Sel fin - sac 25 kg', N'Ingredients', 9.95, 'R', 80),
    (N'CHO-NOI-5', N'Chocolat noir 64 % - pistoles 5 kg', N'Chocolat', 68.50, 'R', 70),
    (N'CHO-LAI-5', N'Chocolat au lait 38 % - pistoles 5 kg', N'Chocolat', 61.20, 'R', 55),
    (N'AMA-POU-1', N'Poudre d''amande - 1 kg', N'Ingredients', 16.80, 'R', 110),
    (N'EMB-SAC-1000', N'Sacs a pain kraft - carton de 1000', N'Emballages', 42.00, 'N', 200),
    (N'EMB-BOI-100', N'Boites patissieres 22 cm - lot de 100', N'Emballages', 37.50, 'N', 140),
    (N'EMB-GOB-500', N'Gobelets carton 20 cl - carton de 500', N'Emballages', 29.90, 'N', 95),
    (N'MAT-MOU-12', N'Moules a tartelette inox - lot de 12', N'Materiel', 54.00, 'N', 30),
    (N'MAT-THE-1', N'Thermometre sonde electronique', N'Materiel', 34.90, 'N', 25),
    (N'BOI-JUS-6', N'Jus de pomme artisanal 1 L - pack de 6', N'Boissons', 15.60, 'R', 60),
    (N'BOI-SOD-24', N'Soda cola 33 cl - pack de 24', N'Boissons', 18.70, 'R', 40),
    (N'TRA-SAN-50', N'Sandwich jambon-beurre emporte - lot de 50 etiquettes', N'Emballages', 8.40, 'N', 500),
    (N'CAF-GRA-1', N'Cafe en grains pur arabica - 1 kg', N'Boissons', 22.50, 'I', 75),
    (N'HUI-TOU-5', N'Huile de tournesol - bidon 5 L', N'Ingredients', 11.20, 'R', 65);
