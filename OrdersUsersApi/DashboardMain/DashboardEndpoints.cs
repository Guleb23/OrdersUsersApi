namespace OrdersUsersApi.DashboardMain
{
    public static class DashboardEndpoints
    {
       
        public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/dashboard").WithTags("Dashboard");
            //Эндпоинты для мини-статистики
            group.MapMiniStatsEndpoints();

            //Эндпоинты для клиентов
            group.MapClientsEndpoints();

            //Эндпоинты для продуктов
            group.MapProductsEndpoints();

            //Эндпоинты для заказа
            group.MapOrdersEndpoints();

            //Эндпоинты для категорий
            group.MapCategoriesEndpoints();

        }
    }

}
