# Saga Orchestrator (.NET)

Este repositório contém um exemplo de **saga orquestradora** implementada em .NET utilizando [MassTransit](https://masstransit-project.com/) e RabbitMQ para coordenar o fluxo de compra de um pacote de viagens. O serviço foi modelado como um *worker service*, pensado para ser consumido por uma BFF que dispara o comando `PurchaseTravelPackage` com os dados da viagem.

## Visão geral da solução

- **Orquestração via Saga:** Um `StateMachine` (`TravelPackageStateMachine`) coordena, de forma assíncrona, as chamadas HTTP para os microsserviços responsáveis por reserva de voo, hotel, carro e processamento de pagamento.
- **Comunicação assíncrona:** A BFF publica um comando no RabbitMQ que é consumido pelo worker. Ao final do fluxo é publicado um evento de sucesso (`TravelPackageCompleted`) ou falha (`TravelPackageFailed`).
- **Integração HTTP:** Cada etapa da reserva é realizada por meio de `HttpClient`s tipados (`HttpFlightReservationService`, `HttpHotelReservationService`, `HttpCarRentalService`, `HttpPaymentService`).
- **Compensação:** Em caso de falha, o serviço `TravelCompensationService` executa as requisições de cancelamento/refund dos microsserviços já chamados.

## Estrutura de diretórios

```
.
├── SagaOrchestrator.sln
├── README.md
└── src
    └── TravelOrchestrator.Worker
        ├── Program.cs
        ├── Worker.cs
        ├── Contracts
        │   └── PurchaseTravelPackage.cs
        ├── Saga
        │   ├── TravelPackageState.cs
        │   └── TravelPackageStateMachine.cs
        ├── Services
        │   ├── *.cs (implementações HTTP + compensação)
        ├── appsettings*.json
        └── TravelOrchestrator.Worker.csproj
```

## Configuração

1. **RabbitMQ:** Ajuste `Host`, `VirtualHost`, `Username` e `Password` no `appsettings.json`.
2. **Endpoints HTTP:** Atualize as URLs das APIs de voo, hotel, carro e pagamento na seção `Services` do `appsettings.json`.
3. **Pacotes NuGet:** O projeto depende de `MassTransit` e `MassTransit.RabbitMQ`.

## Execução local

```bash
# Restaurar dependências
 dotnet restore

# Executar o worker
 dotnet run --project src/TravelOrchestrator.Worker/TravelOrchestrator.Worker.csproj
```

> **Observação:** O ambiente atual não possui o .NET SDK instalado, portanto os comandos acima não puderam ser executados durante a preparação deste exemplo.

## Fluxo principal

1. A BFF publica um `PurchaseTravelPackage` com o `CorrelationId` da compra.
2. A saga reserva voo, hotel e carro em sequência via HTTP.
3. Após todas as reservas, o pagamento é processado.
4. Em caso de sucesso é emitido `TravelPackageCompleted`; em caso de erro qualquer etapa executa compensação e emite `TravelPackageFailed`.

## Próximos passos sugeridos

- Persistir o estado da saga em um armazenamento durável (por exemplo, MongoDB, Entity Framework ou Redis).
- Implementar testes automatizados para o state machine utilizando `MassTransit.Testing`.
- Adicionar políticas de retry e circuit-breaker nos `HttpClient`s com `Polly`.
