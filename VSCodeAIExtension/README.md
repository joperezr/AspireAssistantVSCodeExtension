# VSCode AI Extension

## Description

The VSCode AI Extension adds AI knowledge for Aspire suggestions. It provides a chat participant that can help users with .NET Aspire by teaching concepts with simple descriptions and sample code. The extension uses Qdrant for vector search and an embedding service to generate embeddings for queries.

## Features

- **AI Chat Participant**: A chat participant named "aspire-bot" that helps users with .NET Aspire.
- **Qdrant Integration**: Queries the Qdrant database using embedding vectors to retrieve relevant information.
- **Embedding Generation**: Generates embedding vectors using an HTTP POST request to a local service.

## Requirements

- Node.js
- VS Code 1.95.0 or higher

## Installation

1. Clone the repository.

## Usage

1. Open VS Code.
2. Activate the extension by running any command defined in the package.json.
3. Interact with the "aspire-bot" chat participant to get help with .NET Aspire.

## Contributing

Contributions are welcome! Please open an issue or submit a pull request.

## License

This project is licensed under the MIT License.
