# Definição da arquitetura de Solulções e Projetos .Net

Este documento descreve a arquitetura adotada para o desenvolvimento de soluções e projetos utilizando a plataforma .Net.

## 1. Visão Geral da Arquitetura

- A base da arquitetura é o Domain-Driven Design (DDD), que foca na modelagem do domínio de negócio.
- Uma solução típica é composta por múltiplos projetos, cada um com responsabilidades bem definidas.
- Uma solução deve atender um produto ou serviço.
- Os projetos são organizados em contextos delimitados (Bounded Contexts), os quais são módulos que encapsulam funcionalidades específicas do domínio.
- Projetos executáveis (APIs, Aplicações Web, etc.) consomem os projetos de domínio (os módulos de negócio).
- Cada projeto de módulo é divido em pastas, que podem representar camadas ou agrupamentos lógicos de funcionalidades.

## 2. Estrutura da Solução

Uma solução típica pode ser estruturada da seguinte forma:

```
Company.Solution/
│
├── Apps/                                  # Pasta da solution com os projetos executáveis
│   ├── Company.Solution.Api/              # Projeto da API
│   └── Company.Solution.Web/              # Projeto da Aplicação Web
│
├── Modules/                               # Pasta da solution com os módulos de negócio
│   ├── Company.Solution.ModuleA/          # Módulo A
│   └── Company.Solution.ModuleB/          # Módulo B
│
├── Common/                                # Pasta da solution com projetos compartilhados (Pode ser Shared)
│   ├── Company.Solution.Common/           # Projeto com utilitários e componentes comuns
│   └── Company.Solution.EfMigrations/     # Projeto com migrações do Entity Framework
│
├── Tests/                                 # Pasta da solution com projetos de testes
│   ├── Company.Solution.ModuleA.Tests/    # Testes do Módulo A
│   └── Company.Solution.Api.Tests/        # Testes da API
│
└── Company.Solution.sln                   # Arquivo da solução
```

## 3. Estrutura dos Projetos de Módulo

Cada projeto de módulo pode ser estruturado da seguinte forma:
```
Company.Solution.ModuleA/
│
├── Contracts/                            # Pasta de contratos, objetos de entrada e saída para o módulo
│   ├── UseCaseA/                         # Pasta com contratos do caso de uso A
│   ├── UseCaseB/                         # Pasta com contratos do caso de uso B
│   ├── MyEntities/                       # Pasta com contratos de operações baseadas em CRUD e uma entidade (Create, Read, Update, Delete) (Usar nome da entidade no plural)
│   └── OtherEntities/
│
├── Domain/                               # Camada de domínio
│   ├── Servicos/                         # Pasta com serviços de domínio (que tenha intencionalidade de negócio)
│   ├── ValueObjects/                     # Pasta com objetos de valor
│   ├── Entities/                         # Pasta com entidades do domínio (opcional, pode ser na raiz de Domain)
│   │   ├── AggregateA/                   # Pasta com a entidade raiz do agregado A (opcional, pode não existir ou ficar na raiz de Entities)
│   │   └── AggregateB/                   # Pasta com a entidade raiz do agregado B (opcional, pode não existir ou ficar na raiz de Entities)
│   └── DomainEvents/                     # Pasta com eventos de domínio
│
├── Infrastructure/                       # Camada de infraestrutura
│   ├── Data/                             # Pasta com implementações de acesso a dados
│   │   └── Mappings/                     # Pasta com mapeamentos de entidades para o banco de dados
│   ├── ExternalServices/                 # Pasta com implementações de serviços externos
│   ├── Messaging/                        # Pasta com implementações de mensageria
│   └── Security/                         # Pasta com implementações de segurança
│
├── Application/                          # Camada de aplicação (Pode ser chamada de Handlers)
│   ├── UseCases/                         # Pasta com casos de uso (aplicação de regras de negócio)
│   │   ├── UseCaseA/                     # Pasta com implementações do caso de uso A
│   │   └── UseCaseB/                     # Pasta com implementações do caso de uso B
│   └── Services/                         # Pasta com serviços de aplicação (que orquestram casos de uso)
│
├── Web/
│   └── Apis/                             # Pasta com classes para Minimal APIs ou Controllers (se aplicável)
│   └── Razor/                            # Pasta com componentes Razor/Blazor (se aplicável)
│
└── Company.Solution.ModuleA.csproj       # Arquivo do projeto
```

### 3.1 Definição de Contracts

- Em `Contracts` devem ser criadas pastas para atender os casos de uso.
- Cada caso de uso deve ter seus próprios objetos de entrada e saída.
- Quando o caso de uso são operações baseadas em CRUD e pesquisas para uma entidade, deve ser criada uma pasta com o nome da entidade no plural (ex: `Customers`, `Orders`).
- Os objetos de contratos são:
  - Details: DTO de detalhes para exibição.
  - ValueObjects: Objetos de valor específicos para o contrato.
  - Commands: Objetos de comando para operações que modificam o estado.
  - Queries: Objetos de consulta para operações que recuperam dados sem modificar o estado.
  - Filters: Objetos de filtro para consultas.
  - Results: Objetos de resultado para respostas de operações (para quando Details ou outro objeto não for suficiente).

### 3.2 Definição da Camada de Domínio

- A camada de domínio deve conter toda a lógica de negócio.
- Entidades, objetos de valor, serviços de domínio e eventos de domínio devem ser organizados em suas respectivas pastas.
- Entidades podem ser organizadas em subpastas baseadas em agregados, se aplicável.
- Objetos de valor devem implementar a interface `IValidable` para validação.
- Serviços de domínio devem encapsular lógica de negócio que não pertence a uma única entidade ou objeto de valor.
- Eventos de domínio devem representar eventos significativos que ocorrem dentro do domínio.
- As validações devem ser implementadas utilizando o `SmartValidation` e devem retornar `Result`/`Result<T>` para indicar sucesso ou falha, evitando exceções.
- Entidades devem possuir um construtor protegido sem parâmetros para suportar a desserialização.
- Não selar as classes de entidades para permitir herança, se necessário.
- Utilizar diretivas #nullable disable/restore no construtor protegido sem parâmetros para evitar avisos de nulabilidade.
- A ordem dos membros da classe deve ser: campos privados, construtores, propriedades e métodos.
- Documentar todas as classes, métodos, propriedades, serviços e interfaces com XML documentation para descrever a funcionalidade do domínio.
- Não implementar ou criar interfaces de repositórios, mas usar implementações de repositório das bibliotecas WorkContext, UnitOfWork e Repositories da RoyalCode.
- As entidades devem herdar `Entity<TId>` (ou similar) das bibliotecas da RoyalCode.

### 3.3 Definição da Camada de Infraestrutura

- A camada de infraestrutura deve conter implementações específicas de tecnologia.
- Acesso a dados, serviços externos, mensageria e segurança devem ser organizados em suas respectivas pastas.

#### 3.3.1 Acesso a Dados

- Mapeamentos de entidades para o banco de dados devem ser organizados na pasta `Mappings`.
- Implementações de handlers para queries devem ser feitas utilizando as bibliotecas WorkContext, UnitOfWork e Repositories da RoyalCode.
- Handlers de Queries devem ficar na pasta Infrastructure/Data/Queries.
- Para configuração, deve ser criado um método de extensão para configurar `IWorkContextBuilder`
    - Exemplo:
      ```csharp
      public static IWorkContextBuilder<TDbContext> ConfigureMyModule<TDbContext>(this IWorkContextBuilder<TDbContext> builder)
        where TDbContext : DbContext
      {
        return builder.ConfigureModel(modelBuilder => modelBuilder.MapMyModule())
          .AddRepositories(typeof(MyModuleConfigureWorkContext).Assembly)
          .ConfigureSearches(typeof(MyModuleConfigureWorkContext).Assembly)
          .ConfigureCommands(typeof(MyModuleConfigureWorkContext).Assembly)
          .ConfigureQueries(typeof(MyModuleConfigureWorkContext).Assembly);
      }
      ```

#### 3.3.2 Serviços Externos

- Implementações de serviços externos devem ser organizadas na pasta `ExternalServices`.
- Cada serviço externo deve ter sua própria classe de implementação.
- Utilizar injeção de dependência para gerenciar instâncias de serviços externos.
- Serviços Http devem utilizar HttpClientFactory para criação e gerenciamento de instâncias de HttpClient.
- Cada grupo de Web API externa deve ter sua própria interface e implementação.
- O retorno sempre deverá ser um Result/Result<T>.
- Usar ToResultAsync() das bibliotecas RoyalCode para conversão de respostas Http para Result/Result<T>.
- Devem ser configuradas políticas de retry e circuit breaker utilizando Polly.

#### 3.3.3 Mensageria

- Implementações de mensageria devem ser organizadas na pasta `Messaging`.
- Outras regras a definir conforme o sistema de mensageria adotado.

#### 3.3.4 Segurança

- Implementações de segurança devem ser organizadas na pasta `Security`.
- Outras regras a definir conforme os requisitos de segurança do projeto.

### 3.4 Definição da Camada de Aplicação

- A camada de aplicação deve conter a lógica de orquestração dos casos de uso.
- Casos de uso devem ser organizados na pasta `UseCases`.
- Cada caso de uso deve ter sua própria pasta com as implementações necessárias.
- Serviços de aplicação que orquestram múltiplos casos de uso devem ser organizados na pasta `Services`.
- Os casos de uso devem interagir com a camada de domínio para aplicar regras de negócio.
- As validações devem ser implementadas utilizando o `SmartValidation` e devem retornar `Result`/`Result<T>` para indicar sucesso ou falha, evitando exceções.
- Documentar todas as classes, métodos, propriedades, serviços e interfaces com XML documentation para descrever a funcionalidade do domínio.
- A ordem dos membros da classe deve ser: campos privados, construtores, propriedades e métodos.
- Não implementar ou criar interfaces de repositórios, mas usar implementações de repositório das bibliotecas WorkContext, UnitOfWork e Repositories da RoyalCode.
- Handlers de Commands devem ficar na pasta Application/UseCases/.
- Serviços de aplicação devem ficar na pasta Application/Services/.

### 3.5 Definição da Camada Web

- A camada Web deve conter implementações específicas para APIs ou aplicações web.
- Classes para Minimal APIs ou Controllers devem ser organizadas na pasta `Apis`.
- Componentes Razor/Blazor devem ser organizados na pasta `Razor`, se aplicável.

#### Classe para Minimal APIs

- As classes para Minimal APIs devem ser organizadas na pasta `Apis`.
- Cada grupo de endpoints relacionados deve ter sua própria classe.
- A classe deve ser `static` e conter métodos de extensão para configurar os endpoints.
  - Exemplo: MapMyModuleEndpoints(this IEndpointRouteBuilder app)
- Cada endpoint deve chamar o respectivo handler na camada de aplicação.
- Cada endpoint deve ter um método static que implemente a lógica do endpoint.
- No método de extensão, os endpoints devem ser mapeados em um group específico para o módulo.
  - Exemplo:
    ```csharp
    public static IEndpointRouteBuilder MapMyModuleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/mymodule");
        group.MapGet("/items/{id}", GetItemByIdHandler).WithName("GetItemById");
        group.MapPost("/items", CreateItemHandler).WithName("CreateItem");
        return app;
    }
    ```
- Deve ser priorizado os tipos de retorno `OkMatch`, `NoContentMatch`, `CreatedMatch` das bibliotecas RoyalCode.SmartProblems.AspNetCore para respostas de endpoints.
