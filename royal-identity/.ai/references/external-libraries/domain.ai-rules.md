# Domain (Entities, DomainEvents, Aggregates) — Regras para IA

Regras operacionais para gerar código de domínio com as bibliotecas RoyalCode em projetos .NET.
Para contexto conceitual e explicações, consulte [`domain.md`](domain.md).

> **Verificado contra:** `RoyalCode.Entities`, `RoyalCode.DomainEvents`, `RoyalCode.Aggregates` **0.8.2** — .NET 8 / 9 / 10.
> **Precedência das fontes:** documentação XML/IntelliSense da versão instalada > este arquivo > `domain.md`.
> Com versão divergente, confirme a assinatura no IDE antes de gerar código.

Para persistência (repositórios, save, commands), use [`workcontext.ai-rules.md`](workcontext.ai-rules.md).

## 1. Pacotes e `using`

| Tipo / membro | `using` | Pacote NuGet |
|---|---|---|
| `Entity<>`, `IEntity`, `IHasId<>`, `IHasCode<>`, `IHasGuid`, `IActiveState`, `ISoftDeletable` | `RoyalCode.Entities` | `RoyalCode.Entities` |
| `IDomainEvent`, `DomainEventBase`, `IDomainEventCollection`, `DomainEventCollection`, `IHasEvents`, `ICreationEvent` | `RoyalCode.DomainEvents` | `RoyalCode.DomainEvents` |
| `EventNameAttribute`, `ObservesAttribute`, `EventHandlingContext` | `RoyalCode.DomainEvents.Attributes` | `RoyalCode.DomainEvents` |
| `HasEvent<T>`, `TryGetEvent<T>`, `GetEvents<T>`, `TryGetEvents<T>` | ⚠️ `HexaSamples.SeedWork.Entities` | `RoyalCode.DomainEvents` |
| `IAggregateRoot`, `AggregateRoot<>`, `AggregateRoot<,>` | `RoyalCode.Aggregates` | `RoyalCode.Aggregates` |

`RoyalCode.Aggregates` referencia `RoyalCode.DomainEvents` e `RoyalCode.Entities` transitivamente.

⚠️ As extensões de consulta de eventos (`HasEvent` etc.) exigem `using HexaSamples.SeedWork.Entities;` —
namespace herdado, não dedutível do pacote.

## 2. Regras invioláveis

1. Entidade herda de `Entity<TId>`; raiz de agregado herda de `AggregateRoot<TId>` (ou variantes `<TId,TCode>`).
2. Nunca exponha `set` público para `Id`, `Code`, `IsActive` ou `IsDeleted`. Atribua no construtor ou em
   métodos de domínio (`Id`/`Code` já têm `protected set` nas bases).
3. Registre eventos somente com `AddEvent(...)` de dentro do agregado, após a mudança de estado válida.
4. Nunca publique eventos de dentro da entidade/agregado: sem mensageria, `IServiceProvider`, handlers ou
   qualquer infraestrutura no domínio.
5. Eventos herdam de `DomainEventBase`, são imutáveis (getters somente) e nomeados no passado
   (`OrderCreated`, não `CreateOrder`).
6. Evento que será serializado (outbox, mensageria) deve ter também o construtor
   `(Guid id, DateTimeOffset occurred, ...)` chamando `base(id, occurred)`.
7. Se o evento precisa do ID gerado pelo banco, implemente `ICreationEvent` e capture o ID em `Saved()`;
   não leia `entity.Id` no construtor do evento.
8. `Code` (`IHasCode<TCode>`) é identificador de negócio; não o use como chave/relacionamento no lugar do `Id`.
9. `IHasGuid` é para correlação entre bancos/contextos; não substitui o `Id`.
10. Não mapeie `DomainEvents` na persistência: `builder.Ignore(e => e.DomainEvents)` no mapping EF.
11. Observers de `IDomainEventCollection` devem ser idempotentes: `Observe` reexecuta os eventos já acumulados.

## 3. Matriz de decisão

| Necessidade | Gere |
|---|---|
| entidade com ID | `class X : Entity<TId>` |
| entidade com ID + código de negócio | `class X : Entity<TId, TCode>` |
| raiz de agregado (coleta eventos) | `class X : AggregateRoot<TId>` |
| raiz de agregado com código | `class X : AggregateRoot<TId, TCode>` |
| habilitar/desabilitar sem excluir | implemente `IActiveState` + métodos `Enable()`/`Disable()` |
| exclusão lógica | implemente `ISoftDeletable` + método `SoftDelete()` |
| identificador global entre contextos | implemente `IHasGuid` |
| DTO que referencia entidade (merge/update) | `class XDto : IHasId<TId>` |
| fato de domínio | `sealed class XHappened : DomainEventBase` |
| evento com ID gerado pelo banco | evento também implementa `ICreationEvent` |
| nome estável de evento p/ serialização | `[EventName("contexto.evento")]` |
| método observador declarativo | `[Observes(EventHandlingContext...)]` (execução é da infraestrutura) |
| checar eventos coletados (testes/serviços) | `HasEvent<T>()` / `TryGetEvent<T>(out ...)` |

## 4. Assinaturas (cheat-sheet)

```csharp
// Entities
abstract class Entity<TId> : IEntity<TId>            { TId Id { get; protected set; } }
abstract class Entity<TId, TCode> : Entity<TId>, IHasCode<TCode> { TCode Code { get; protected set; } }
interface IEntity { }
interface IEntity<out TId> : IEntity, IHasId<TId> { }
interface IHasId<out TId>     { TId Id { get; } }
interface IHasCode<out TCode> { TCode Code { get; } }
interface IHasGuid            { Guid Guid { get; } }
interface IActiveState        { bool IsActive { get; } }
interface ISoftDeletable      { bool IsDeleted { get; } }

// DomainEvents
interface IDomainEvent : IHasId<Guid> { DateTimeOffset Occurred { get; } }
abstract class DomainEventBase : IDomainEvent
{
    protected DomainEventBase();                                 // novo Id + Occurred (DateTimeOffset.Now)
    protected DomainEventBase(Guid id, DateTimeOffset occurred); // desserialização
}
interface IDomainEventCollection : ICollection<IDomainEvent>
{
    void Observe(Action<IDomainEvent> observerAction);      // replay dos acumulados + futuros
    void RemoveObserver(Action<IDomainEvent> observerAction);
}
class DomainEventCollection : IDomainEventCollection { }    // [CollectionBuilder]: aceita [ ]
interface IHasEvents     { IDomainEventCollection? DomainEvents { get; set; } }
interface ICreationEvent { void Saved(); }                  // chamado pela infra após o save

// Aggregates
interface IAggregateRoot : IEntity, IHasEvents { }
abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot<TId>
{
    IDomainEventCollection? DomainEvents { get; set; }
    protected void AddEvent(IDomainEvent evt);              // cria a coleção sob demanda
}
abstract class AggregateRoot<TId, TCode> : AggregateRoot<TId>, IHasCode<TCode> { }

// Extensions de IHasEvents (using HexaSamples.SeedWork.Entities)
bool HasEvent<TEvent>();
bool TryGetEvent<TEvent>([NotNullWhen(true)] out TEvent? @event);
IEnumerable<TEvent> GetEvents<TEvent>();
bool TryGetEvents<TEvent>(out IEnumerable<TEvent> events);
```

## 5. Padrões canônicos

Entidade:

```csharp
using RoyalCode.Entities;

public class Person : Entity<int>
{
    public string Name { get; private set; }

    public Person(string name)
    {
        Name = name;
    }

    public void Rename(string name) => Name = name;
}
```

Agregado com evento:

```csharp
using RoyalCode.Aggregates;
using RoyalCode.DomainEvents;

public sealed class Order : AggregateRoot<Guid>
{
    public string Number { get; private set; }
    public bool IsShipped { get; private set; }

    public Order(string number)
    {
        Id = Guid.NewGuid();
        Number = number;
        AddEvent(new OrderCreated(Id, Number));
    }

    public void Ship()
    {
        if (IsShipped)
            return;

        IsShipped = true;
        AddEvent(new OrderShipped(Id));
    }
}
```

Evento serializável (dois construtores):

```csharp
public sealed class OrderCreated : DomainEventBase
{
    public Guid OrderId { get; }
    public string Number { get; }

    public OrderCreated(Guid orderId, string number)
    {
        OrderId = orderId;
        Number = number;
    }

    public OrderCreated(Guid id, DateTimeOffset occurred, Guid orderId, string number)
        : base(id, occurred)
    {
        OrderId = orderId;
        Number = number;
    }
}
```

Evento com ID gerado pelo banco (`ICreationEvent`):

```csharp
public sealed class ProductCreated : DomainEventBase, ICreationEvent
{
    private readonly Product product;

    public int ProductId { get; private set; }

    public ProductCreated(Product product)
    {
        this.product = product;
    }

    public void Saved()
    {
        ProductId = product.Id;   // ID já gerado pelo banco
    }
}
```

Estados:

```csharp
public sealed class Feature : Entity<Guid>, IActiveState
{
    public bool IsActive { get; private set; } = true;
    public void Enable() => IsActive = true;
    public void Disable() => IsActive = false;
}

public sealed class Document : Entity<Guid>, ISoftDeletable
{
    public bool IsDeleted { get; private set; }
    public void SoftDelete() => IsDeleted = true;
}
```

Teste verificando eventos coletados:

```csharp
using HexaSamples.SeedWork.Entities;   // ⚠️ namespace das extensões

var order = new Order("N-123");

Assert.True(order.HasEvent<OrderCreated>());
Assert.True(order.TryGetEvent<OrderCreated>(out var created));
Assert.Equal("N-123", created.Number);
```

Mapping EF da raiz de agregado:

```csharp
public void Configure(EntityTypeBuilder<Order> builder)
{
    builder.HasKey(o => o.Id);
    builder.Ignore(o => o.DomainEvents);   // a coleção não é estado persistente
}
```

## 6. Anti-padrões

```csharp
// ❌ setter público de identidade              // ✅ construtor/método de domínio
public new int Id { get; set; }                 public Person(int id) { Id = id; }

// ❌ publicar evento no agregado               // ✅ coletar; a infra despacha após o save
public void Ship(IEventBus bus)                 public void Ship()
{ bus.Publish(new OrderShipped(Id)); }          { IsShipped = true; AddEvent(new OrderShipped(Id)); }

// ❌ evento mutável / nome no imperativo       // ✅ imutável, no passado
class CreateOrder : DomainEventBase             sealed class OrderCreated : DomainEventBase
{ public Guid OrderId { get; set; } }           { public Guid OrderId { get; } }

// ❌ ID do banco capturado no construtor       // ✅ ICreationEvent.Saved()
AddEvent(new ProductCreated(Id));               AddEvent(new ProductCreated(this));

// ❌ Add direto na coleção fora do agregado    // ✅ método de domínio + AddEvent protegido
order.DomainEvents!.Add(new OrderShipped(id));  order.Ship();

// ❌ presumir DomainEvents não-nulo            // ✅ AddEvent inicializa; leitores usam extensões
order.DomainEvents.Count > 0                    order.HasEvent<OrderShipped>()
```

Regras adicionais:

- Não herde de `DomainEventCollection` para adicionar lógica de publicação; observers pertencem à infraestrutura.
- Não use `DateTime.Now`/`UtcNow` em eventos: `DomainEventBase` já define `Occurred`; para comparar instantes,
  use `Occurred.ToUniversalTime()`.
- Não crie repositórios para entidades internas do agregado; apenas para a raiz
  (ver [`workcontext.ai-rules.md`](workcontext.ai-rules.md)).
- Não adicione dependência de EF/DI/mensageria nos projetos de domínio.
- `[Observes]`/`[EventName]` são declarativos: não gere código presumindo que as bibliotecas de domínio
  descobrem e executam os observadores.

## 7. Checklist antes de entregar o código

- [ ] `using` conferidos na tabela da §1 (inclusive `HexaSamples.SeedWork.Entities` para as extensões).
- [ ] Entidades herdam de `Entity<>`/`AggregateRoot<>`; sem setters públicos de `Id`/`Code`/estados.
- [ ] Eventos: `sealed`, imutáveis, nome no passado, herdam `DomainEventBase`.
- [ ] Eventos serializáveis têm o construtor `(id, occurred, ...)`.
- [ ] Eventos dependentes de ID do banco implementam `ICreationEvent`.
- [ ] `AddEvent` chamado apenas dentro do agregado, após mudança de estado válida.
- [ ] Nenhuma publicação/infraestrutura dentro do domínio.
- [ ] Mapping EF ignora `DomainEvents`.
- [ ] Estados alterados por métodos de domínio (`Enable`, `Disable`, `SoftDelete`).
