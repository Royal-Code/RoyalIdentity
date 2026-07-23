# RoyalIdentity.Migrations

Runner separado para migrations e seed de Configuration. Ele nunca é chamado por `RoyalIdentity.Server` e
não aplica nada sem provider e conexão explícitos. O namespace `configuration-*` da CLI deixa a futura conexão
Operational independente, embora ambas possam apontar para o mesmo banco físico.

Em ambientes automatizados, prefira fornecer a conexão por variável de ambiente para que ela não apareça na
linha de comando:

```powershell
$env:ROYALIDENTITY_CONFIGURATION_DB = 'Data Source=C:\data\royalidentity.db'
dotnet run --project RoyalIdentity.Migrations -- `
  --configuration-provider sqlite `
  --configuration-connection-env ROYALIDENTITY_CONFIGURATION_DB
```

O seed é opcional e separado entre produto e demo. Todo seed exige escolha explícita de protector; `Plain`
nunca é default:

```powershell
$env:ROYALIDENTITY_CONFIGURATION_DB = '<connection supplied by deployment secret>'
$env:ROYALIDENTITY_CONFIGURATION_AES_KEY = '<base64 AES key supplied by KMS or key vault>'
dotnet run --project RoyalIdentity.Migrations -- `
  --configuration-provider postgresql `
  --configuration-connection-env ROYALIDENTITY_CONFIGURATION_DB `
  --seed product `
  --server-admin-redirect-uri https://admin.example.com/signin-oidc `
  --server-admin-redirect-uri https://admin.example.com/callback `
  --key-protector aes `
  --aes-key-env ROYALIDENTITY_CONFIGURATION_AES_KEY
```

O seed `product` (e, por consequência, `all`) exige ao menos um
`--server-admin-redirect-uri`. A opção é repetível, aceita qualquer URI absoluta suportada pelo cliente e não
possui default implícito: redirects são dados do ambiente e precisam ser escolhidos pelo operador. URLs
localhost permanecem somente no seed `demo` deste runner.

Também estão disponíveis `--key-protector plain` (opt-in inseguro, com warning) e
`--key-protector data-protection`, que exige `--data-protection-key-ring` e aceita
`--data-protection-app-name`. Em produção, o key ring precisa ser persistente, compartilhado pelas instâncias e
protegido em repouso.

`--seed demo` adiciona apenas o realm/clients demo; `--seed all` combina produto e demo. A segunda execução é
idempotente. O runner retorna `64` para uso inválido, `1` para falha de migration/seed e `0` para sucesso.

Os protectors são selecionáveis no provisionamento. Trocar o protector de uma base que já contém signing keys
exige uma migração/reproteção própria; o runner não converte material persistido entre protectors. Essa rotação
pertence ao futuro plano de KMS.

Para produção, os scripts revisáveis em `scripts/sql/configuration/` são o caminho preferido. O futuro Aspire
executará este runner como workload/container separado dos hosts, conforme `.ai/backlogs/backlog-001.md`.
