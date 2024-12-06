// The module 'vscode' contains the VS Code extensibility API
// Import the module and reference it with the alias vscode in your code below
import * as vscode from 'vscode';
import * as dotenv from 'dotenv';

// Load environment variables from .env file
dotenv.config();

// This method is called when your extension is activated
// Your extension is activated the very first time the command is executed
export async function activate(context: vscode.ExtensionContext) {

    // Use the console to output diagnostic information (console.log) and errors (console.error)
    // This line of code will only be executed once when your extension is activated
    console.log('Congratulations, your extension "vscodeaiextension" is now active!');

    // The command has been defined in the package.json file
    // Now provide the implementation of the command with registerCommand
    // The commandId parameter must match the command field in package.json
    const tutor = await vscode.chat.createChatParticipant('vscodeaiextension.chatParticipant', handler);

    tutor.iconPath = vscode.Uri.joinPath(context.extensionUri, 'images/crush.png');

    //tutor.iconPath = vscode.Uri.joinPath(context.extensionUri, 'tutor.jpeg');
}

// Function to create a Qdrant client instance
async function createQdrantClient() {
    const { QdrantClient } = await import('@qdrant/js-client-rest');
    return new QdrantClient({
        url: "http://localhost:6333/", // This should be flowing from environment that is set by the apphost
        apiKey: "MySecretApiKey" // This should be flowing from environment that is set by the apphost
    });
}

// Function to query the Qdrant database
async function queryQdrant(vector: number[]) {
    try {
        const qdrantClient = await createQdrantClient();
        const response = await qdrantClient.search("sections", {
            vector: vector,
            limit: 10 // Number of results to return
        });

        console.log('Qdrant query response:', response);
        return response;
    } catch (error) {
        console.error('Error querying Qdrant:', error);
    }
}

const BASE_PROMPT =
    'You are a helpful code tutor that teaches .NET Aspire. Your job is to teach the user with simple descriptions and sample code of the concept. Respond with a guided overview of the concept in a series of messages. If the user asks a non-programming question, politely decline to respond.';

const MODEL_SELECTOR: vscode.LanguageModelChatSelector = {
    vendor: 'copilot',
    family: 'gpt-4o'
};

// Function to generate an embedding vector using an HTTP POST request
async function generateEmbedding(text: string): Promise<number[]> {
    const response = await fetch('https://localhost:7284/embedding', { // This should be flowing from environment that is set by the apphost
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify({ input: text })
    });

    if (!response.ok) {
        throw new Error('Failed to generate embedding');
    }

    interface EmbeddingResponse {
        result: number[];
    }

    const data: EmbeddingResponse = await response.json() as EmbeddingResponse;
    if (data && Array.isArray(data.result)) {
        return data.result;
    } else {
        throw new Error('Invalid response format');
    }
}

// Define a chat handler
const handler: vscode.ChatRequestHandler = async (
    request: vscode.ChatRequest,
    _context: vscode.ChatContext,
    stream: vscode.ChatResponseStream,
    token: vscode.CancellationToken
) => {
    try {
        let prompt = BASE_PROMPT;

        const [model] = await vscode.lm.selectChatModels(MODEL_SELECTOR);

        if (model) {
            const messages = [vscode.LanguageModelChatMessage.User(prompt)];

            messages.push(vscode.LanguageModelChatMessage.User('You have the personality of Crush the turtle from Finding Nemo and so you answer using words like bro and dude.'));

            // Generate an embedding vector for the request.prompt
            const embedding = await generateEmbedding(request.prompt);

            // Query the Qdrant database using the embedding vector
            const qdrantResponse = await queryQdrant(embedding);

            // Add the retrieved information to the messages collection
            if (qdrantResponse) {
                const additionalInfo = qdrantResponse.map((item: any) => item.payload.Content).join('\n');
                messages.push(vscode.LanguageModelChatMessage.User("Base your answers on the following info:\n\n" + additionalInfo));
            }

			messages.push(vscode.LanguageModelChatMessage.User(request.prompt));

            const chatResponse = await model.sendRequest(messages, {}, token);

            for await (const fragment of chatResponse.text) {
                stream.markdown(fragment);
            }
        }
    } catch (error) {
        console.error('Error in chat handler:', error);
    }
};

// This method is called when your extension is deactivated
export function deactivate() {}
