# Documentação da API de Domínio (Entities, DomainEvents, Aggregates)

Esta documentação apresenta os conceitos, funcionalidades e exemplos práticos para usar as bibliotecas de domínio nesta solução.
Serve também como referência para ferramentas de IA (ex.: GitHub Copilot) compreenderem e gerarem código correto com base na API das bibliotecas.

Projetos alvo: .NET 8, .NET 9 e .NET 10.

Escopo:
- `RoyalCode.Entities`
- `RoyalCode.DomainEvents`
- `RoyalCode.Aggregates`

Para persistência (WorkContext/UnitOfWork/Repositories), consulte `.docs/workcontext.md`.

## 1. Introdução

Os componentes de domínio fornecem abstrações simples e consistentes para modelar entidades com identificadores, 
códigos de negócio, estados, e para compor agregados que coletam eventos de domínio. 
As APIs são minimalistas, focadas em segurança (setters protegidos), imutabilidade e integração com camadas de infraestrutura de publicação de eventos.

Benefícios principais:
- Entidades tipadas por ID com base reutilizável.
- Suporte a códigos de negócio (`Code`) distintos do ID.
- Contratos para estados (`IsActive`) e exclusão lógica (`IsDeleted`).
- Coleção de eventos de domínio em agregados com observadores.
- APIs simples e neutras de infraestrutura, prontas para integração.

---

## 2. RoyalCode.Entities

Fundação para modelagem de entidades. Fornece contratos e implementações básicas para `Id`, `Code`, `Guid`, estado ativo e exclusão lógica.

Principais tipos:
- `IEntity` / `IEntity<TId>`: marca uma entidade e o tipo do seu identificador.
- `Entity<TId>`: base com propriedade `Id` (set protegido).
- `Entity<TId, TCode>`: base com `Id` e `Code` (set protegido).
- `IHasId<TId>`: expõe `Id` para entidades/DTOs.
- `IHasCode<TCode>`: expõe `Code` (identificador amigável e único, distinto do ID).
- `IHasGuid`: expõe `Guid` global (não substitui o ID; útil para referência cruzada entre bancos/contextos).
- `IActiveState`: expõe `IsActive` para habilitar/desabilitar sem deletar.
- `ISoftDeletable`: expõe `IsDeleted` para exclusão lógica.

Assinaturas relevantes (resumo):
- `public abstract class Entity<TId> : IEntity<TId> { public TId Id { get; protected set; } }`
- `public abstract class Entity<TId,TCode> : Entity<TId>, IHasCode<TCode> { public TCode Code { get; protected set; } }`
- `public interface IHasGuid { Guid Guid { get; } }`
- `public interface IActiveState { bool IsActive { get; } }`
- `public interface ISoftDeletable { bool IsDeleted { get; } }`

Quando usar:
- Herde de `Entity<TId>` para qualquer entidade de domínio com ID.
- Use `Entity<TId, TCode>` quando existir também um código de negócio único.
- Implemente `IHasGuid`, `IActiveState` e/ou `ISoftDeletable` conforme os requisitos do domínio.

Exemplo básico:
```csharp
using RoyalCode.Entities;

public class Person : Entity<int>
{
    public string Name { get; private set; } = string.Empty;

    public Person(int id, string name)
    {
        Id = id;
        Name = name;
    }
}
```

Exemplo com `Entity<TId, TCode>`:
```csharp
using RoyalCode.Entities;

public class CatalogItem : Entity<Guid, string>
{
    public string Name { get; private set; } = string.Empty;

    public CatalogItem(Guid id, string code, string name)
    {
        Id = id;
        Code = code;
        Name = name;
    }
}
```

DTO associado usando `IHasId<TId>`:
```csharp
using RoyalCode.Entities;

public class PersonDto : IHasId<int>
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
```

---

## 3. RoyalCode.DomainEvents

Modelagem de eventos de domínio e sua coleção observável. Integrado a agregados para registrar mudanças relevantes do domínio.

Principais tipos:
- `IDomainEvent` (herda `IHasId<Guid>`): evento com `Id` e `Occurred` (`DateTimeOffset`).
- `DomainEventBase`: base abstrata que gera `Id` e `Occurred` automaticamente (ou permite definir em sobrecarga para desserialização).
- `IDomainEventCollection` (`ICollection<IDomainEvent>`): coleção com `Observe(Action<IDomainEvent>)` e `RemoveObserver(...)`.
- `DomainEventCollection`: implementação padrão com observadores disparados a cada `Add` e reexecução para eventos já acumulados.
- `IHasEvents`: contrato para entidades que expõem `IDomainEventCollection? DomainEvents { get; set; }`.

Assinaturas relevantes (resumo):
- `public interface IDomainEvent : IHasId<Guid> { DateTimeOffset Occurred { get; } }`
- `public abstract class DomainEventBase : IDomainEvent { public Guid Id { get; } public DateTimeOffset Occurred { get; } }`
- `public interface IDomainEventCollection : ICollection<IDomainEvent> { void Observe(Action<IDomainEvent>); void RemoveObserver(Action<IDomainEvent>); }`

Exemplo de evento de domínio:
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
}
```

Observando eventos já criados e futuros:
```csharp
var collection = new DomainEventCollection();
collection.Observe(evt => Console.WriteLine($"Observed: {evt.GetType().Name} at {evt.Occurred}"));

collection.Add(new OrderCreated(Guid.NewGuid(), "N-123"));
```

---

## 4. RoyalCode.Aggregates

Modelagem de Agregados (DDD). Define a raiz do agregado e integra a coleta de eventos de domínio.

Principais tipos:
- `IAggregateRoot` / `IAggregateRoot<TId>`: marca a raiz do agregado e o tipo do ID.
- `AggregateRoot<TId>`: base que herda de `Entity<TId>` e inclui `IDomainEventCollection? DomainEvents` + método protegido `AddEvent(IDomainEvent)`.
- `AggregateRoot<TId, TCode>`: versão com `Code` além do ID e dos eventos.

Assinaturas relevantes (resumo):
- `public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot<TId> { protected void AddEvent(IDomainEvent evt) { DomainEvents ??= []; DomainEvents.Add(evt); } }`
- `public abstract class AggregateRoot<TId,TCode> : AggregateRoot<TId>, IHasCode<TCode> { public TCode Code { get; protected set; } }`

Exemplo simples:
```csharp
using RoyalCode.Aggregates;
using RoyalCode.DomainEvents;

public sealed class Order : AggregateRoot<Guid>
{
    public string Number { get; private set; }

    public Order(string number)
    {
        Id = Guid.NewGuid();
        Number = number;
        AddEvent(new OrderCreated(Id, Number));
    }
}

public sealed class OrderCreated : DomainEventBase
{
    public Guid OrderId { get; }
    public string Number { get; }
    public OrderCreated(Guid orderId, string number) { OrderId = orderId; Number = number; }
}
```

Exemplo com `AggregateRoot<TId, TCode>`:
```csharp
using RoyalCode.Aggregates;
using RoyalCode.DomainEvents;

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

public sealed class ProductCreated : DomainEventBase
{
    public Guid ProductId { get; }
    public string Code { get; }
    public ProductCreated(Guid productId, string code) { ProductId = productId; Code = code; }
}
```

---

## 5. Integração com Persistência e Publicação de Eventos

- `DomainEvents` é apenas a coleção; a publicação/dispatch é responsabilidade de infraestrutura (WorkContext/UnitOfWork/Outbox).
- Em contextos EF, bibliotecas deste repositório acoplam observers e despacham eventos após `SaveChanges`.
- Consulte `.docs/workcontext.md` e projetos `RoyalCode.WorkContext.*`, `RoyalCode.UnitOfWork.*` e `RoyalCode.Events.Outbox.*`.

---

## 6. Boas Práticas

- Mantenha invariantes do agregado dentro da raiz e dispare eventos com `AddEvent` após mudanças significativas.
- Não exponha `set` público para `Id`/`Code`; use construtores/métodos de domínio.
- `IHasGuid` para correlação entre bancos/contextos.
- Prefira `ISoftDeletable` e `IActiveState` para cenários de (des)ativação e exclusão lógica.
- Evite lógica de publicação de eventos dentro do agregado; mantenha coleta apenas.
- Eventos devem ser pequenos, descritivos e serializáveis quando necessário.

---

## 7. Referência de API

Entities:
- `IEntity`, `IEntity<TId>`, `Entity<TId>`, `Entity<TId,TCode>`, `IHasId<TId>`, `IHasCode<TCode>`, `IHasGuid`, `IActiveState`, `ISoftDeletable`.

DomainEvents:
- `IDomainEvent`, `DomainEventBase`, `IDomainEventCollection`, `DomainEventCollection`, `IHasEvents`.

Aggregates:
- `IAggregateRoot`, `IAggregateRoot<TId>`, `AggregateRoot<TId>`, `AggregateRoot<TId,TCode>`.

---

## 8. Exemplos de uso em Serviços

Criando e coletando eventos ao modificar estado:
```csharp
public sealed class OrderService
{
    public Order Create(string number)
    {
        return new Order(number); // Order cria OrderCreated via AddEvent
    }
}
```

Desabilitando uma entidade usando `IActiveState`:
```csharp
public sealed class Feature : Entity<Guid>, IActiveState
{
    public bool IsActive { get; private set; } = true;
    public void Disable() => IsActive = false;
}
```

Exclusão lógica com `ISoftDeletable`:
```csharp
public sealed class Document : Entity<Guid>, ISoftDeletable
{
    public bool IsDeleted { get; private set; }
    public void SoftDelete() => IsDeleted = true;
}
```

---

## 9. Resumo

As bibliotecas de domínio fornecem uma base enxuta e consistente para modelar entidades e agregados em DDD, com ID e `Code`, estados (`IsActive`, `IsDeleted`) e coleta de eventos de domínio em `DomainEvents`. Os agregados usam `AddEvent` para registrar eventos e delegam despacho à infraestrutura. As APIs são simples, tipadas e compatíveis com múltiplos targets (.NET 8/9/10).

---

## 10. Instruções para Ferramentas de IA (GitHub Copilot)

Diretrizes de geração alinhadas às seções anteriores:
- Entidades
  - Gere classes que herdam de `Entity<TId>` e, se necessário, `Entity<TId,TCode>`.
  - Não use setters públicos para `Id`/`Code`; atribua via construtor ou métodos internos.
  - Para DTOs, implemente `IHasId<TId>`.
- Agregados e Eventos
  - Raiz do agregado deve herdar de `AggregateRoot<TId>` (ou `<TId,TCode>`).
  - Registre eventos com `AddEvent(new MyEvent(...))`; eventos podem herdar `DomainEventBase`.
  - Não implemente publicação dentro do agregado; use a coleção `DomainEvents` e deixe a infraestrutura publicar.
- Integração com Persistência
  - Deixe `DomainEvents` serem processados por WorkContext/UnitOfWork/Outbox após `SaveChanges`.
- Padrões de prompt
  - “Crie uma entidade com `Entity<Guid>` e agregue evento com `AggregateRoot<Guid>` ao criar.”
  - “Implemente um evento herdando `DomainEventBase` com campos necessários e registre no agregado.”
  - “Modele `ISoftDeletable` e `IActiveState` em entidades e use métodos para alterar estado sem expor setters.”

Evite
- Setters públicos em `Id`/`Code`.
- Publicar eventos diretamente de dentro das entidades.
- Misturar persistência/infraestrutura no domínio; mantenha coleções e contratos.

