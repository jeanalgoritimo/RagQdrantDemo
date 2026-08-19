RagQdrantDemo — RAG com .NET, OpenAI e Qdrant







Demonstração prática de RAG — Retrieval-Augmented Generation construída em C# e .NET 9. A aplicação transforma documentos em embeddings, armazena os vetores no Qdrant, recupera o conteúdo semanticamente mais próximo de uma pergunta e envia esse contexto para um modelo da OpenAI gerar a resposta.

Projeto educacional para demonstrar o ciclo básico de indexação, busca vetorial e geração aumentada por recuperação.

O que é RAG?

Um modelo de linguagem possui conhecimento geral, mas não conhece automaticamente os documentos privados ou atualizados de uma organização. O RAG acrescenta uma etapa de recuperação antes da geração da resposta:

um documento é convertido em um vetor numérico, chamado embedding;

o vetor e o conteúdo original são armazenados em um banco vetorial;

a pergunta do usuário também é convertida em embedding;

o Qdrant procura o documento semanticamente mais semelhante;

o conteúdo recuperado é enviado como contexto para o modelo de chat;

o modelo responde usando as informações encontradas.

Arquitetura

flowchart TD
    D["Documento"] --> E["Embedding OpenAI"]
    E --> Q["Qdrant"]
    U["Pergunta do usuário"] --> PE["Embedding da pergunta"]
    PE --> Q
    Q --> C["Contexto recuperado"]
    C --> G["GPT-4o mini"]
    U --> G
    G --> R["Resposta contextualizada"]

Funcionalidades

O menu da aplicação permite:

listar os documentos armazenados no Qdrant;

visualizar o conteúdo de um documento;

salvar localmente uma cópia do conteúdo recuperado;

inserir novos documentos e gerar seus embeddings;

atualizar documentos, recalculando o vetor correspondente;

fazer perguntas utilizando busca vetorial e GPT;

criar automaticamente a coleção meus_documentos quando ela não existir.

Tecnologias e modelos

Componente

Uso no projeto

.NET 9

Plataforma da aplicação Console

C#

Implementação do fluxo RAG

OpenAI SDK 2.13.0

Embeddings e geração das respostas

Qdrant.Client 1.19.0

Comunicação gRPC com o banco vetorial

Qdrant

Armazenamento e busca por similaridade

text-embedding-3-small

Geração de vetores com 1.536 dimensões

gpt-4o-mini

Geração da resposta a partir do contexto

Docker

Execução local recomendada do Qdrant

Pré-requisitos

.NET SDK 9;

Docker Desktop ou uma instalação local do Qdrant;

uma chave válida da API da OpenAI com saldo disponível;

acesso à internet para chamadas à API e restauração dos pacotes NuGet.

Confira as instalações:

dotnet --version
docker --version

Executando o projeto

1. Clone o repositório

git clone https://github.com/jeanalgoritimo/RagQdrantDemo.git
cd RagQdrantDemo

O branch padrão atual do repositório é master.

2. Inicie o Qdrant

docker run --name qdrant-rag \
  -p 6333:6333 \
  -p 6334:6334 \
  -v qdrant_storage:/qdrant/storage \
  -d qdrant/qdrant

Portas utilizadas:

Porta

Finalidade

6333

API REST e dashboard do Qdrant

6334

comunicação gRPC utilizada pela aplicação

Com o container em execução, o dashboard fica disponível em:

http://localhost:6333/dashboard

Para iniciar novamente um container já criado:

docker start qdrant-rag

3. Configure a chave da OpenAI

O código atual contém apenas o placeholder abaixo em Program.cs:

string openAiKey = "Sua chave";

Para um teste exclusivamente local, substitua o valor pela sua chave sem realizar commit dessa alteração.

Para uma abordagem segura, recomenda-se alterar o código para ler uma variável de ambiente:

string openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? throw new InvalidOperationException("A variável OPENAI_API_KEY não foi configurada.");

No PowerShell:

$env:OPENAI_API_KEY="sua-chave-aqui"

No Prompt de Comando:

set OPENAI_API_KEY=sua-chave-aqui

No Linux ou macOS:

export OPENAI_API_KEY="sua-chave-aqui"

Nunca publique uma chave real no GitHub. Caso uma chave seja exposta, revogue-a imediatamente e gere outra.

4. Restaure e execute

dotnet restore
dotnet run

A aplicação criará a coleção meus_documentos com distância cosseno e vetores de 1.536 dimensões.

Utilizando o menu

1. Listar e Baixar/Visualizar Arquivos
2. Inserir Novo Documento
3. Atualizar Documento Existente
4. Fazer Pergunta via RAG (Busca Vetorial + GPT)
5. Sair

Inserindo um documento

Escolha a opção 2, informe um nome e digite o conteúdo. A aplicação:

envia o texto para o modelo de embeddings;

calcula o próximo identificador numérico;

grava no Qdrant o vetor e os payloads arquivo e conteudo.

Exemplo:

Nome: politica-ferias.txt
Conteúdo: As férias devem ser solicitadas com no mínimo 30 dias de antecedência.

Fazendo uma pergunta

Escolha a opção 4 e pergunte algo relacionado aos documentos indexados:

Com quanto tempo de antecedência devo solicitar minhas férias?

O sistema pesquisa o ponto mais próximo, adiciona seu conteúdo ao prompt e solicita a resposta ao modelo de chat.

Estrutura do projeto

RagQdrantDemo/
├── Program.cs                 # Menu, indexação, busca e geração da resposta
├── RagQdrantDemo.csproj       # .NET 9 e dependências NuGet
├── RagQdrantDemo.sln          # Solução do Visual Studio
├── documentos/                # Textos de exemplo
│   ├── beneficios.txt.txt
│   ├── horario-trabalho.txt.txt
│   ├── onboarding.txt.txt
│   ├── politica-ferias.txt.txt
│   └── suporte-ti.txt.txt
└── .github/
    └── copilot-instructions.md

Sobre a pasta documentos

O arquivo de projeto copia a pasta documentos para o diretório de saída durante a compilação. Entretanto, a versão atual da aplicação não percorre nem indexa esses arquivos automaticamente.

Para armazená-los no Qdrant, use a opção 2 do menu e informe manualmente o conteúdo de cada documento. A entrada atual utiliza Console.ReadLine(), portanto funciona melhor com textos em uma única linha.

Como os dados são armazenados

Cada ponto salvo no Qdrant contém:

ID numérico
├── vector: embedding com 1.536 posições
└── payload
    ├── arquivo: nome do documento
    └── conteudo: texto original

A coleção utiliza Distance.Cosine, que compara a orientação dos vetores para encontrar conteúdos semanticamente próximos.

Limitações atuais

Recupera somente um documento por pergunta (limit: 1);

não divide documentos grandes em chunks;

não utiliza score mínimo de relevância;

não exibe a fonte nem a pontuação junto da resposta;

não impede que o modelo responda além do contexto recuperado;

a inclusão manual lê apenas uma linha de conteúdo;

a listagem recupera no máximo 100 pontos;

não há exclusão de documentos pelo menu;

não há tratamento centralizado de indisponibilidade, timeout ou limite da API;

não há testes automatizados, telemetria ou avaliação da qualidade das respostas;

não há autenticação no Qdrant local.

Próximas evoluções

Ler a chave da OpenAI exclusivamente por configuração segura;

importar automaticamente os arquivos da pasta documentos;

adicionar chunking com sobreposição para documentos extensos;

recuperar múltiplos trechos e aplicar score mínimo;

apresentar fontes e grau de similaridade na resposta;

instruir o modelo a assumir quando o contexto não for suficiente;

suportar PDF, DOCX e outros formatos;

adicionar exclusão e filtros por metadados;

criar uma API ASP.NET Core e uma interface de chat;

incluir logs estruturados, resiliência e controle de custos;

criar testes e avaliações automatizadas de RAG;

executar aplicação e Qdrant com Docker Compose.

Boas práticas para produção

Armazene credenciais em variáveis de ambiente, Secret Manager ou Azure Key Vault;

proteja o Qdrant com autenticação, TLS e rede privada;

valide o tamanho e o formato dos documentos antes da indexação;

remova ou masque dados pessoais e informações confidenciais;

registre fontes, versão do documento e data de indexação nos metadados;

aplique autorização antes de recuperar documentos de cada usuário;

monitore tokens, latência, erros e custos da API;

estabeleça políticas de retenção e tratamento compatíveis com a LGPD;

trate o conteúdo recuperado como dado não confiável para reduzir riscos de prompt injection.

Solução de problemas

Nenhum documento foi encontrado

Cadastre primeiro pelo menos um documento com a opção 2. A presença de arquivos na pasta documentos não significa que eles já estejam armazenados no Qdrant.

Não foi possível conectar ao Qdrant

Verifique o container e a porta gRPC:

docker ps
docker logs qdrant-rag

A aplicação espera o Qdrant em 127.0.0.1:6334.

Erro de autenticação da OpenAI

Confirme se a chave é válida e se o código está lendo o valor correto. Não inclua espaços ou aspas extras no valor.

HTTP 429 ou limite excedido

Verifique saldo, cota e limites de requisição da conta. Aguarde antes de repetir chamadas e considere implementar retry com backoff exponencial.

Dimensão de vetor incompatível

A coleção foi criada para embeddings de 1.536 dimensões. Ao trocar o modelo de embedding, confirme a dimensão gerada e recrie uma coleção compatível.

Autor

Desenvolvido por Jean Paiva da Silva.

GitHub: @jeanalgoritimo

LinkedIn: Jean Silva

Repositório: jeanalgoritimo/RagQdrantDemo

Se este projeto ajudou você a compreender RAG, embeddings e bancos vetoriais, considere deixar uma ⭐ no repositório.
