# Documentação da API de Domínio (Entities, DomainEvents, Aggregates)

As bibliotecas de domínio fornecem a base para modelar entidades, agregados e eventos de domínio em projetos .NET,
seguindo DDD com APIs minimalistas: identificadores tipados, códigos de negócio, estados e coleta observável de eventos.

Este é o guia conceitual e prático. Para instruções objetivas destinadas a ferramentas de IA, consulte também
[`domain.ai-rules.md`](domain.ai-rules.md).

> **Verificado contra:** `RoyalCode.Entities`, `RoyalCode.DomainEvents` e `RoyalCode.Aggregates` **0.8.2** — .NET 8, .NET 9 e .NET 10.
> **Precedência das fontes:** documentação XML/IntelliSense da versão instalada > [`domain.ai-rules.md`](domain.ai-rules.md) > este guia.
> Se a versão do pacote for diferente, confirme as assinaturas no IDE antes de gerar código.

Para persistência (WorkContext/UnitOfWork/Repositories), consulte [`workcontext.md`](workcontext.md).

Sumário

1. Visão geral e conceitos
2. Pacotes, namespaces e instalação
3. Entidades (`RoyalCode.Entities`)
4. Eventos de domínio (`RoyalCode.DomainEvents`)
5. Agregados (`RoyalCode.Aggregates`)
6. Integração com persistência e publicação de eventos
7. Referência rápida da API
8. Erros comuns
9. Boas práticas

## 1. Visão geral e conceitos

| Conceito | Papel |
|---|---|
| `Entity<TId>` | base para entidades com identificador tipado e setter protegido |
| `Code` | identificador de negócio único e amigável, distinto do ID (`IHasCode<TCode>`) |
| `Guid` global | correlação da mesma entidade entre bancos/contextos (`IHasGuid`) |
| estados | ativação (`IActiveState`) e exclusão lógica (`ISoftDeletable`) sem apagar registros |
| `IDomainEvent` | fato relevante do domínio, com `Id` e `Occurred` |
| `IDomainEventCollection` | coleção observável onde o agregado acumula eventos |
| `AggregateRoot<TId>` | raiz do agregado; coleta eventos com `AddEvent` |
| `ICreationEvent` | contrato para eventos que dependem de ID gerado pelo banco |

O princípio central: **o domínio coleta eventos, a infraestrutura publica**. As entidades registram eventos em
`DomainEvents` e componentes de persistência (UnitOfWork/WorkContext/Outbox) observam a coleção e despacham no
momento adequado (normalmente ao salvar).

```text
agregado muda de estado
        │ AddEvent(...)
        ▼
IDomainEventCollection ── Observe(...) ──► infraestrutura (UoW/Outbox)
        │                                        │
        ▼                                        ▼
 eventos acumulados                    despacho após SaveChanges
```

## 2. Pacotes, namespaces e instalação

```bash
dotnet add package RoyalCode.Aggregates
```

`RoyalCode.Aggregates` referencia `RoyalCode.DomainEvents` e `RoyalCode.Entities` transitivamente.
Instale apenas `RoyalCode.Entities` quando o projeto não usa agregados nem eventos.

| Tipo / membro | `using` (namespace) | Pacote NuGet |
|---|---|---|
| `Entity<>`, `IEntity`, `IHasId<>`, `IHasCode<>`, `IHasGuid`, `IActiveState`, `ISoftDeletable` | `RoyalCode.Entities` | `RoyalCode.Entities` |
| `IDomainEvent`, `DomainEventBase`, `IDomainEventCollection`, `DomainEventCollection`, `IHasEvents`, `ICreationEvent` | `RoyalCode.DomainEvents` | `RoyalCode.DomainEvents` |
| `EventNameAttribute`, `ObservesAttribute`, `EventHandlingContext` | `RoyalCode.DomainEvents.Attributes` | `RoyalCode.DomainEvents` |
| `HasEvent<T>`, `TryGetEvent<T>`, `GetEvents<T>`, `TryGetEvents<T>` (extensions de `IHasEvents`) | ⚠️ `HexaSamples.SeedWork.Entities` | `RoyalCode.DomainEvents` |
| `IAggregateRoot`, `IAggregateRoot<>`, `AggregateRoot<>`, `AggregateRoot<,>` | `RoyalCode.Aggregates` | `RoyalCode.Aggregates` |

⚠️ As extensões de consulta de eventos (`HasEvent`, `TryGetEvent`, `GetEvents`, `TryGetEvents`) vivem no namespace
**`HexaSamples.SeedWork.Entities`** — um nome herdado que não é dedutível a partir do pacote. Sem esse `using`,
os métodos não aparecem no IntelliSense.

## 3. Entidades (`RoyalCode.Entities`)

### 3.1 Contratos

```csharp
public interface IEntity { }
public interface IEntity<out TId> : IEntity, IHasId<TId> { }

public interface IHasId<out TId>     { TId Id { get; } }
public interface IHasCode<out TCode> { TCode Code { get; } }
public interface IHasGuid            { Guid Guid { get; } }
public interface IActiveState        { bool IsActive { get; } }
public interface ISoftDeletable      { bool IsDeleted { get; } }
```

Todos os contratos expõem apenas getters. Quem decide como o valor muda (construtor, método de domínio) é a
implementação — nunca exponha `set` público para `Id`, `Code` ou estados.

### 3.2 Classes base

```csharp
public abstract class Entity<TId> : IEntity<TId>
{
    public TId Id { get; protected set; }
}

public abstract class Entity<TId, TCode> : Entity<TId>, IHasCode<TCode>
{
    public TCode Code { get; protected set; }
}
```

Exemplo básico:

```csharp
using RoyalCode.Entities;

public class Person : Entity<int>
{
    public string Name { get; private set; } = string.Empty;

    public Person(string name)
    {
        Name = name;
    }
}
```

Com código de negócio (`Entity<TId, TCode>`):

```csharp
public class CatalogItem : Entity<Guid, string>
{
    public string Name { get; private set; }

    public CatalogItem(string code, string name)
    {
        Id = Guid.NewGuid();
        Code = code;
        Name = name;
    }
}
```

O `Code` é um identificador **de negócio**: único, estável e legível (SKU, número de pedido, sigla).
Ele não substitui o `Id` — chaves e relacionamentos continuam pelo `Id`.

### 3.3 Estados: ativação e exclusão lógica

Implemente os contratos e altere o estado por métodos de domínio:

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

Use `IActiveState` para habilitar/desabilitar sem remover; use `ISoftDeletable` quando o registro deve ser
tratado como excluído sem deleção física. Filtros globais (ex.: EF `HasQueryFilter`) são responsabilidade
da camada de persistência.

### 3.4 `IHasGuid` para correlação entre contextos

Quando a mesma entidade existe em vários bancos/contextos com IDs locais diferentes, `IHasGuid` mantém um
identificador global único:

```csharp
public class Customer : Entity<int>, IHasGuid
{
    public Guid Guid { get; private set; } = Guid.NewGuid();
}
```

Na camada de persistência, entidades `IHasGuid` ganham busca por Guid automaticamente
(`FindByGuidAsync`, ver [`workcontext.md`](workcontext.md) §5).

### 3.5 DTOs com `IHasId<TId>`

`IHasId<TId>` também serve para DTOs que referenciam entidades. Os repositórios usam esse contrato no
`Merge` (atualização por modelo):

```csharp
public class PersonDto : IHasId<int>
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
```

## 4. Eventos de domínio (`RoyalCode.DomainEvents`)

### 4.1 `IDomainEvent` e `DomainEventBase`

```csharp
public interface IDomainEvent : IHasId<Guid>
{
    DateTimeOffset Occurred { get; }
}
```

`DomainEventBase` implementa o contrato com dois construtores:

```csharp
protected DomainEventBase();                                  // gera Id novo e Occurred = DateTimeOffset.Now
protected DomainEventBase(Guid id, DateTimeOffset occurred);  // para desserialização
```

Evento típico — imutável, pequeno e com os dois construtores quando precisa ser serializado:

```csharp
using RoyalCode.DomainEvents;

public sealed class OrderCreated : DomainEventBase
{
    public Guid OrderId { get; }
    public string Number { get; }

    public OrderCreated(Guid orderId, string number)
    {
        OrderId = orderId;
        Number = number;
    }

    // construtor de desserialização: preserva Id e Occurred originais
    public OrderCreated(Guid id, DateTimeOffset occurred, Guid orderId, string number)
        : base(id, occurred)
    {
        OrderId = orderId;
        Number = number;
    }
}
```

Sem o segundo construtor, um evento desserializado (outbox, mensageria, armazenamento) ganharia `Id` e
`Occurred` novos, quebrando idempotência e auditoria.

Observação: `Occurred` usa `DateTimeOffset.Now` (offset local). O `DateTimeOffset` carrega o offset e permite
conversão correta para UTC, mas em comparações e ordenações persista/compare sempre pelo instante
(`ToUniversalTime()`), não pelo relógio de parede.

### 4.2 `IDomainEventCollection` e observadores

```csharp
public interface IDomainEventCollection : ICollection<IDomainEvent>
{
    void Observe(Action<IDomainEvent> observerAction);
    void RemoveObserver(Action<IDomainEvent> observerAction);
}
```

`DomainEventCollection` é a implementação padrão. Comportamento dos observadores:

- todo evento adicionado (`Add`) é imediatamente entregue aos observadores registrados;
- ao registrar um observador com `Observe`, os eventos **já acumulados são reexecutados** para ele, um a um;
- `RemoveObserver` remove o observador.

```csharp
var collection = new DomainEventCollection();
collection.Add(new OrderCreated(orderId, "N-123"));

// o observador recebe também o evento já adicionado acima
collection.Observe(evt => Console.WriteLine($"{evt.GetType().Name} at {evt.Occurred}"));
```

Consequência prática: observadores precisam ser idempotentes ou registrados antes do primeiro `Add`.
Esse replay é o que permite à infraestrutura (UnitOfWork) conectar-se a agregados que já criaram eventos
no construtor.

Os dois tipos possuem `[CollectionBuilder]`, então expressões de coleção funcionam:

```csharp
IDomainEventCollection events = [];                       // vazia
IDomainEventCollection one = [new OrderCreated(id, "N")]; // com eventos
```

### 4.3 `IHasEvents`

```csharp
public interface IHasEvents
{
    IDomainEventCollection? DomainEvents { get; set; }
}
```

O contrato é anulável e com `set` público **de propósito**: a coleção só é criada quando o primeiro evento
acontece, e a infraestrutura pode atribuir uma coleção própria (ex.: uma coleção conectada ao contexto de
persistência) antes de o agregado gerar eventos.

### 4.4 Consultando eventos (`HasEventsExtensions`)

```csharp
using HexaSamples.SeedWork.Entities; // ⚠️ namespace não-óbvio (ver §2)

if (order.HasEvent<OrderCreated>()) { /* ... */ }

if (order.TryGetEvent<OrderCreated>(out var created))
    Console.WriteLine(created.Number);

IEnumerable<OrderCreated> all = order.GetEvents<OrderCreated>();

if (order.TryGetEvents<OrderCreated>(out var events)) { /* ... */ }
```

Úteis em testes (verificar que uma operação gerou o evento esperado) e em serviços de aplicação que precisam
reagir a eventos coletados antes do save.

### 4.5 `ICreationEvent`: eventos que dependem de ID gerado pelo banco

Quando o ID da entidade é gerado pelo banco (identity/sequence), um evento criado no construtor captura o ID
**default** (0, `Guid.Empty`). `ICreationEvent` resolve isso: o componente de persistência notifica o evento
após o save, quando o ID já existe:

```csharp
public sealed class ProductCreated : DomainEventBase, ICreationEvent
{
    private readonly Product product;

    public int ProductId { get; private set; }

    public ProductCreated(Product product)
    {
        this.product = product;
    }

    // chamado pela infraestrutura após SaveChanges, com o ID já gerado
    public void Saved()
    {
        ProductId = product.Id;
    }
}
```

Regras do contrato:

- o componente ligado ao unit of work chama `Saved()` **depois** de persistir as entidades;
- somente após essa notificação os eventos podem ser despachados para mensageria;
- se os eventos são armazenados, armazene-os após a notificação (com transação aberta, quando o banco suportar).

### 4.6 Atributos informativos (`EventNameAttribute`, `ObservesAttribute`)

```csharp
using RoyalCode.DomainEvents.Attributes;

[EventName("orders.created")]
public sealed class OrderCreated : DomainEventBase { /* ... */ }

public class OrderProjectionHandler
{
    [Observes(EventHandlingContext.InNewTransaction)]
    public void When(OrderCreated evt) { /* ... */ }
}
```

- `EventNameAttribute` define um nome estável para o evento (útil para serialização/roteamento).
- `ObservesAttribute` declara um método observador e o contexto de tratamento:
  - `InSameTransaction`: tratado na mesma transação das entidades que geraram o evento;
  - `InNewTransaction` (padrão): tratado após o commit da transação original.
- Ambos são **declarativos**: a descoberta e execução ficam a cargo do componente de infraestrutura adotado
  pelo projeto (dispatcher, outbox); as bibliotecas de domínio não executam nada sozinhas.

## 5. Agregados (`RoyalCode.Aggregates`)

### 5.1 Contratos e classes base

```csharp
public interface IAggregateRoot : IEntity, IHasEvents { }
public interface IAggregateRoot<out TId> : IAggregateRoot, IEntity<TId> { }

public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot<TId>
{
    public IDomainEventCollection? DomainEvents { get; set; }

    protected void AddEvent(IDomainEvent evt)
    {
        DomainEvents ??= [];
        DomainEvents.Add(evt);
    }
}

public abstract class AggregateRoot<TId, TCode> : AggregateRoot<TId>, IHasCode<TCode>
{
    public TCode Code { get; protected set; }
}
```

`AddEvent` é `protected`: somente o próprio agregado registra eventos, sempre a partir de uma mudança de
estado válida. A coleção é criada sob demanda — agregados que nunca geram eventos não alocam nada.

### 5.2 Exemplo canônico

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

Com código de negócio:

```csharp
public sealed class ProductAggregate : AggregateRoot<Guid, string>
{
    public string Name { get; private set; }

    public ProductAggregate(string code, string name)
    {
        Id = Guid.NewGuid();
        Code = code;
        Name = name;
        AddEvent(new ProductCreated(Id, Code));
    }
}
```

### 5.3 O que pertence (e o que não pertence) à raiz do agregado

- Invariantes do agregado moram na raiz; objetos internos são alterados por métodos dela.
- Eventos descrevem **fatos ocorridos** (`OrderShipped`), não intenções (`ShipOrder`).
- A raiz **não publica** eventos: nada de mensageria, `IServiceProvider` ou handlers dentro do agregado.
- Repositórios existem apenas para raízes de agregado (regra aplicada na camada de persistência).

## 6. Integração com persistência e publicação de eventos

- `DomainEvents` é apenas a coleção; despacho é responsabilidade da infraestrutura.
- Em contextos EF deste repositório, o WorkContext/UnitOfWork pode observar as coleções e processar eventos
  no ciclo de `SaveChanges` — consulte [`workcontext.md`](workcontext.md).
- Para publicação confiável entre serviços, use o padrão Outbox (`RoyalCode.Outbox.*` /
  `RoyalCode.Events.Outbox.*`): os eventos são gravados na mesma transação das entidades e publicados depois.
- Eventos `ICreationEvent` são notificados (`Saved()`) após o save e antes do despacho/armazenamento (§4.5).

O mapeamento EF deve **ignorar** `DomainEvents` — a coleção não é estado persistente da entidade:

```csharp
builder.Ignore(e => e.DomainEvents);
```

## 7. Referência rápida da API

Entities (`RoyalCode.Entities`):

```csharp
Entity<TId>                    // Id { get; protected set; }
Entity<TId, TCode>             // + Code { get; protected set; }
IEntity; IEntity<out TId>
IHasId<out TId>; IHasCode<out TCode>; IHasGuid
IActiveState                   // bool IsActive { get; }
ISoftDeletable                 // bool IsDeleted { get; }
```

DomainEvents (`RoyalCode.DomainEvents`):

```csharp
IDomainEvent : IHasId<Guid>            // DateTimeOffset Occurred { get; }
DomainEventBase()                      // novo Id + Occurred
DomainEventBase(Guid id, DateTimeOffset occurred)   // desserialização

IDomainEventCollection : ICollection<IDomainEvent>
    void Observe(Action<IDomainEvent> observerAction);      // replay dos acumulados + futuros
    void RemoveObserver(Action<IDomainEvent> observerAction);
DomainEventCollection                  // implementação padrão, suporta [ ]

IHasEvents                             // IDomainEventCollection? DomainEvents { get; set; }
ICreationEvent                         // void Saved();
```

Extensions de `IHasEvents` (⚠️ `using HexaSamples.SeedWork.Entities`):

```csharp
bool HasEvent<TEvent>();
bool TryGetEvent<TEvent>([NotNullWhen(true)] out TEvent? @event);
IEnumerable<TEvent> GetEvents<TEvent>();
bool TryGetEvents<TEvent>(out IEnumerable<TEvent> events);
```

Aggregates (`RoyalCode.Aggregates`):

```csharp
IAggregateRoot : IEntity, IHasEvents
IAggregateRoot<out TId> : IAggregateRoot, IEntity<TId>
AggregateRoot<TId> : Entity<TId>       // DomainEvents + protected AddEvent(IDomainEvent)
AggregateRoot<TId, TCode>              // + Code { get; protected set; }
```

Atributos (`RoyalCode.DomainEvents.Attributes`):

```csharp
[EventName("nome-do-evento")]                          // classe de evento
[Observes(EventHandlingContext.InNewTransaction)]      // método observador (informativo)
enum EventHandlingContext { InSameTransaction, InNewTransaction }
```

## 8. Erros comuns

### 8.1 Setter público em `Id`/`Code`

```csharp
// ❌ Quebra o encapsulamento: qualquer código altera a identidade.
public class Person : Entity<int>
{
    public new int Id { get; set; }
}

// ✅ Atribua no construtor ou em método de domínio; o set é protected.
public class Person : Entity<int>
{
    public Person(int id) { Id = id; }
}
```

### 8.2 Publicar eventos de dentro do agregado

```csharp
// ❌ O agregado não conhece infraestrutura.
public void Ship(IEventBus bus)
{
    IsShipped = true;
    bus.Publish(new OrderShipped(Id));
}

// ✅ Colete com AddEvent; a infraestrutura despacha depois do save.
public void Ship()
{
    IsShipped = true;
    AddEvent(new OrderShipped(Id));
}
```

### 8.3 Evento serializável sem construtor de desserialização

```csharp
// ❌ Ao desserializar, ganha Id e Occurred novos.
public sealed class OrderShipped(Guid orderId) : DomainEventBase;

// ✅ Ofereça o construtor com (id, occurred) — ver §4.1.
```

### 8.4 Capturar ID gerado pelo banco no construtor do evento

```csharp
// ❌ product.Id ainda é 0 quando o evento é criado.
public Product() { AddEvent(new ProductCreated(Id)); }

// ✅ Implemente ICreationEvent e extraia o ID em Saved() — ver §4.5.
```

### 8.5 Esquecer o `using` das extensões de eventos

`order.HasEvent<OrderCreated>()` não compila sem `using HexaSamples.SeedWork.Entities;`.
O namespace não segue o padrão `RoyalCode.*` (§2).

### 8.6 Observador registrado depois e efeitos duplicados

`Observe` reexecuta os eventos já acumulados para o novo observador. Se o mesmo observador for registrado
duas vezes, processa os eventos duas vezes. Registre uma única vez e trate os handlers como idempotentes.

### 8.7 Mapear `DomainEvents` como propriedade persistente

O EF pode tentar mapear `DomainEvents` como navegação. Ignore-a no mapeamento (§6);
eventos persistidos pertencem ao outbox, não à tabela da entidade.

## 9. Boas práticas

- Herde de `Entity<TId>` para entidades e de `AggregateRoot<TId>` para raízes de agregado; use as variantes
  `<TId, TCode>` quando existir código de negócio.
- Mantenha invariantes na raiz do agregado e registre eventos com `AddEvent` após mudanças de estado válidas.
- Nomeie eventos no passado (`OrderCreated`, `PaymentApproved`) e mantenha-os pequenos, imutáveis e serializáveis.
- Implemente o construtor de desserialização em eventos que atravessam processos.
- Use `ICreationEvent` quando o evento precisa de ID gerado pelo banco.
- Use `IActiveState`/`ISoftDeletable` para (des)ativação e exclusão lógica; altere estados por métodos de domínio.
- Use `IHasGuid` para correlação entre bancos/contextos; nunca como substituto do `Id`.
- Não referencie infraestrutura no domínio: os pacotes de domínio não dependem de EF, DI ou mensageria — mantenha
  seu código de domínio igual.
- Em testes, verifique a coleta de eventos com `HasEvent<T>()`/`TryGetEvent<T>(out ...)`.

Para geração de código por IA, use [`domain.ai-rules.md`](domain.ai-rules.md), que contém regras imperativas,
matriz de decisão, receitas e checklist.
