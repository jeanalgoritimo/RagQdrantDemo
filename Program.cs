using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OpenAI.Chat;
using OpenAI.Embeddings;
using Qdrant.Client;
using Qdrant.Client.Grpc;

public class Program
{
    private const string CollectionName = "meus_documentos";

    public static async Task Main()
    {
        string openAiKey = "Sua chave";

        var embeddingClient = new EmbeddingClient("text-embedding-3-small", openAiKey);
        var chatClient = new ChatClient("gpt-4o-mini", openAiKey);

        // 1. Conexão com o Qdrant
        var qdrantClient = new QdrantClient("127.0.0.1", 6334);

        // 2. Garante que a Coleção existe
        bool collectionExists = await qdrantClient.CollectionExistsAsync(CollectionName);
        if (!collectionExists)
        {
            await qdrantClient.CreateCollectionAsync(
                CollectionName,
                new VectorParams { Size = 1536, Distance = Distance.Cosine }
            );
        }

        // --- MENU INTERATIVO ---
        while (true)
        {
            Console.WriteLine("\n==========================================");
            Console.WriteLine("       PAINEL QDRANT - SELECIONE          ");
            Console.WriteLine("==========================================");
            Console.WriteLine("1. Listar e Baixar/Visualizar Arquivos");
            Console.WriteLine("2. Inserir Novo Documento");
            Console.WriteLine("3. Atualizar Documento Existente");
            Console.WriteLine("4. Fazer Pergunta via RAG (Busca Vetorial + GPT)");
            Console.WriteLine("5. Sair");
            Console.Write("\nOpção: ");

            string opcao = Console.ReadLine() ?? "";

            switch (opcao)
            {
                case "1":
                    await ListarEBaixarArquivos(qdrantClient);
                    break;
                case "2":
                    await InserirNovoDocumento(qdrantClient, embeddingClient);
                    break;
                case "3":
                    await AtualizarDocumento(qdrantClient, embeddingClient);
                    break;
                case "4":
                    await ProcessarPerguntaRag(qdrantClient, embeddingClient, chatClient);
                    break;
                case "5":
                    return;
            }
        }
    }

    // 1. LISTAR E VISUALIZAR / BAIXAR
    private static async Task ListarEBaixarArquivos(QdrantClient qdrantClient)
    {
        var pontos = await ObterTodosOsPontos(qdrantClient);
        if (pontos.Count == 0) return;

        Console.WriteLine($"\n--- Arquivos Cadastrados no Qdrant ({pontos.Count}) ---");
        for (int i = 0; i < pontos.Count; i++)
        {
            string nomeArquivo = pontos[i].Payload.TryGetValue("arquivo", out var val) ? val.StringValue : "Sem nome";
            Console.WriteLine($"[{i + 1}] ID Qdrant: {pontos[i].Id.Num} | Arquivo: {nomeArquivo}");
        }

        Console.Write("\nDigite o número do arquivo para visualizar (ou 0 para voltar): ");
        if (int.TryParse(Console.ReadLine(), out int escolha) && escolha > 0 && escolha <= pontos.Count)
        {
            var pontoSelecionado = pontos[escolha - 1];
            string nome = pontoSelecionado.Payload["arquivo"].StringValue;
            string conteudo = pontoSelecionado.Payload["conteudo"].StringValue;

            Console.WriteLine($"\n--- [CONTEÚDO DO ARQUIVO: {nome}] ---");
            Console.WriteLine(conteudo);
            Console.WriteLine("------------------------------------------");

            Console.Write("\nDeseja baixar/salvar este conteúdo localmente? (s/n): ");
            if (Console.ReadLine()?.ToLower() == "s")
            {
                string pastaDownloads = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "downloads");
                Directory.CreateDirectory(pastaDownloads);

                string caminhoDownload = Path.Combine(pastaDownloads, $"baixado_{nome}");
                await File.WriteAllTextAsync(caminhoDownload, conteudo);
                Console.WriteLine($"\n[✓] Arquivo salvo em: {caminhoDownload}");
            }
        }
    }

    // 2. INSERIR NOVO DOCUMENTO
    private static async Task InserirNovoDocumento(QdrantClient qdrantClient, EmbeddingClient embeddingClient)
    {
        Console.WriteLine("\n--- INSERIR NOVO DOCUMENTO ---");
        Console.Write("Digite o nome do arquivo (ex: politicas_ferias.txt): ");
        string nomeArquivo = Console.ReadLine() ?? "documento_novo.txt";

        Console.WriteLine("Digite ou cole o conteúdo do documento:");
        string conteudo = Console.ReadLine() ?? "";

        if (string.IsNullOrWhiteSpace(conteudo))
        {
            Console.WriteLine("[!] Conteúdo vazio. Operação cancelada.");
            return;
        }

        Console.WriteLine("\nGerando embedding no OpenAI...");
        OpenAIEmbedding embedding = await embeddingClient.GenerateEmbeddingAsync(conteudo);

        // Calcula um novo ID único (maior ID atual + 1)
        var pontosExistentes = await ObterTodosOsPontos(qdrantClient);
        ulong novoId = pontosExistentes.Count > 0
            ? pontosExistentes.Max(p => p.Id.Num) + 1
            : 1;

        var novoPonto = new PointStruct
        {
            Id = novoId,
            Vectors = embedding.ToFloats().ToArray(),
            Payload = { ["conteudo"] = conteudo, ["arquivo"] = nomeArquivo }
        };

        await qdrantClient.UpsertAsync(CollectionName, new[] { novoPonto });
        Console.WriteLine($"[✓] Documento '{nomeArquivo}' inserido com sucesso! ID Qdrant: {novoId}");
    }

    // 3. ATUALIZAR DOCUMENTO EXISTENTE
    private static async Task AtualizarDocumento(QdrantClient qdrantClient, EmbeddingClient embeddingClient)
    {
        var pontos = await ObterTodosOsPontos(qdrantClient);
        if (pontos.Count == 0) return;

        Console.WriteLine("\n--- ATUALIZAR DOCUMENTO EXISTENTE ---");
        for (int i = 0; i < pontos.Count; i++)
        {
            string nomeArquivo = pontos[i].Payload.TryGetValue("arquivo", out var val) ? val.StringValue : "Sem nome";
            Console.WriteLine($"[{i + 1}] ID: {pontos[i].Id.Num} | Arquivo: {nomeArquivo}");
        }

        Console.Write("\nDigite o número do documento que deseja atualizar (ou 0 para cancelar): ");
        if (int.TryParse(Console.ReadLine(), out int escolha) && escolha > 0 && escolha <= pontos.Count)
        {
            var pontoParaAtualizar = pontos[escolha - 1];
            ulong idQdrant = pontoParaAtualizar.Id.Num;
            string nomeAtual = pontoParaAtualizar.Payload["arquivo"].StringValue;

            Console.WriteLine($"\nVocê está atualizando: ID {idQdrant} - {nomeAtual}");
            Console.WriteLine("Digite o novo conteúdo atualizado para o documento:");
            string novoConteudo = Console.ReadLine() ?? "";

            if (string.IsNullOrWhiteSpace(novoConteudo))
            {
                Console.WriteLine("[!] Conteúdo não pode ser vazio.");
                return;
            }

            Console.WriteLine("\nRecalculando vetor (Embedding) na OpenAI...");
            OpenAIEmbedding novoEmbedding = await embeddingClient.GenerateEmbeddingAsync(novoConteudo);

            // Ao dar Upsert mantendo o mesmo ID, o Qdrant sobrescreve o ponto antigo
            var pontoAtualizado = new PointStruct
            {
                Id = idQdrant,
                Vectors = novoEmbedding.ToFloats().ToArray(),
                Payload = { ["conteudo"] = novoConteudo, ["arquivo"] = nomeAtual }
            };

            await qdrantClient.UpsertAsync(CollectionName, new[] { pontoAtualizado });
            Console.WriteLine($"[✓] Documento '{nomeAtual}' (ID: {idQdrant}) atualizado com sucesso no Qdrant!");
        }
    }

    // 4. CONSULTA RAG
    private static async Task ProcessarPerguntaRag(QdrantClient qdrantClient, EmbeddingClient embeddingClient, ChatClient chatClient)
    {
        Console.Write("\nDigite sua pergunta: ");
        string pergunta = Console.ReadLine() ?? "";
        if (string.IsNullOrWhiteSpace(pergunta)) return;

        OpenAIEmbedding embeddingPergunta = await embeddingClient.GenerateEmbeddingAsync(pergunta);

        var searchResults = await qdrantClient.SearchAsync(
            CollectionName,
            vector: embeddingPergunta.ToFloats().ToArray(),
            limit: 1
        );

        if (searchResults.Count == 0)
        {
            Console.WriteLine("Nenhum contexto relevante encontrado.");
            return;
        }

        string contextoRecuperado = searchResults[0].Payload["conteudo"].StringValue;

        string prompt = $"""
        Responda utilizando o contexto recuperado do banco vetorial:

        --- CONTEXTO ---
        {contextoRecuperado}

        --- PERGUNTA ---
        {pergunta}
        """;

        ChatCompletion resposta = await chatClient.CompleteChatAsync([new UserChatMessage(prompt)]);
        Console.WriteLine($"\n[Resposta RAG]:\n{resposta.Content[0].Text}");
    }

    // MÉTODO AUXILIAR
    private static async Task<IReadOnlyList<RetrievedPoint>> ObterTodosOsPontos(QdrantClient qdrantClient)
    {
        var scrollResult = await qdrantClient.ScrollAsync(
            CollectionName,
            limit: 100,
            payloadSelector: true,
            vectorsSelector: false
        );

        if (scrollResult.Result.Count == 0)
        {
            Console.WriteLine("\n[!] Nenhum documento encontrado no Qdrant.");
        }

        return scrollResult.Result;
    }
}