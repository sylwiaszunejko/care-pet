# Build an IoT App with CSharp

## Introduction

In this section, we will walk you through the CarePet commands and explain the code behind them.

As explained in [Getting Started with CarePet](/getting-started.md), the project is structured as follows:
- Migrate (/cmd/migrate) - Creates the CarePet keyspace and tables.
- Collar (/cmd/sensor) - Simulates a pet's collar by generating the pet's health data and pushing the data into the storage.
- Server (/cmd/server) - REST API service for tracking the pets’ health state.