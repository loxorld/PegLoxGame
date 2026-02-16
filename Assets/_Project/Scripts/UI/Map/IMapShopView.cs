using System.Collections.Generic;

public interface IMapShopView
{
    void ShowShop(MapDomainService.ShopOutcome shopOutcome, IReadOnlyList<ShopService.ShopOptionData> options);
}