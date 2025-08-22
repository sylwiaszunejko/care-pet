# Build an IoT App with CSharp

## Introduction

In this section, we will walk you through the CarePet commands and explain the code behind them.

As explained in [Getting Started with CarePet](/getting-started.md), the project is structured as follows:
- Migrate (CarePet.Migrate) - Creates the CarePet keyspace and tables.
- Collar (CarePet.Sensor) - Simulates a pet's collar by generating the pet's health data and pushing the data into the storage.
- Server (CarePet.Server.App) - REST API service for tracking the pets’ health state.