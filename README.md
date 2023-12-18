# PriceNegotiationApp
## Technologies
[<img align="left" alt="Csharp" width="36px" src="https://cdn.jsdelivr.net/gh/devicons/devicon/icons/csharp/csharp-original.svg" style="padding-right:10px;"/>][csharp]
[<img align="left" alt="dotnet" width="36px" src="https://upload.wikimedia.org/wikipedia/commons/thumb/7/7d/Microsoft_.NET_logo.svg/2048px-Microsoft_.NET_logo.svg.png" style="padding-right:10px;"/>][dotnet]
[<img align="left" alt="entityframework" width="36px" style="padding-right:10px;" src="https://github.com/lukegor/PriceNegotiationApp/assets/105490868/bad63060-6eed-47e9-bb65-1ee03f4cccdd"/>][entityframework]
[<img align="left" alt="XUnit" width="36px" src="https://avatars.githubusercontent.com/u/2092016?s=200&v=4" style="padding-right:10px;"/>][XUnit]

<br>

## Project Description
<b>PriceNegotiationApp</b> is a backend project built using ASP.NET Core Web API. The application also utilizes Entity Framework and xUnit. It is designed exclusively as an API-only service. It enables customers to negotiate prices with online store staff, offering data management through CRUD operations. Up to 3 proposal retries are allowed within the negotiation process. If a proposal is more than double the base price, it's auto-rejected.

Customers may register, log in, request data about products, open up price negotiation regarding a product, propose a price for 3 times. Shop staff may add products, view negotiations, accept or deny the proposed price. Administrator is privileged to manage data, inluding user data.


[csharp]: https://pl.wikipedia.org/wiki/C_Sharp
[dotnet]: https://learn.microsoft.com/pl-pl/dotnet/
[entityframework]: https://learn.microsoft.com/pl-pl/ef/
[XUnit]: https://github.com/xunit/xunit



## API Endpoints

![PriceNegotiationApp_Swagger-UI_Endpoints](https://github.com/lukegor/PriceNegotiationApp/assets/105490868/2ec1841b-2ed2-45d5-9283-f0acbf8c5aba)

## API Documentation

The documentation in .yaml format is located in the <b>openapi-docs.yaml</b> file.<br/>
To visualize and interact with the documentation using Swagger UI, upload the <b>openapi-docs.yaml</b> file on the <b>https://editor-next.swagger.io/</b> or <b>https://redocly.github.io/redoc/</b>.

## Default accounts
| Login                 | Password   | Role  |
|-----------------------|------------|-------|
| admin@app.com         | Admin123!  | Admin |
| Staff1@app.com        | Staff123!  | Staff |

## Security

The API is secured with JWT authorization. Some non-secretive HTTP GET endpoints have been allowed anonymously. The other require authorization based on user role.

## License

This project is licensed under the [Apache License 2.0](https://opensource.org/license/apache-2-0/)
