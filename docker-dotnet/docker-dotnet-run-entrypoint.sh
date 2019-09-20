#!/bin/bash

echo "Starting dotent"
until dotnet DroHub.dll; do
    echo "dotnet failed. Trying again in 3 seconds..."
    sleep 3
done