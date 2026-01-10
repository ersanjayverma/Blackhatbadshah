#!/bin/bash

# Blackhatbadshah Setup Script
# This script helps you set up the environment for the application

set -e

echo "=================================="
echo "Blackhatbadshah Setup Script"
echo "=================================="
echo ""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Check if .env file exists
ENV_FILE="infra/dockerCompose/.env"

if [ -f "$ENV_FILE" ]; then
    echo -e "${YELLOW}Warning: $ENV_FILE already exists.${NC}"
    read -p "Do you want to overwrite it? (y/N): " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo "Setup cancelled. Using existing .env file."
        exit 0
    fi
fi

# Copy .env.example to .env
echo "Creating .env file from template..."
cp infra/dockerCompose/.env.example "$ENV_FILE"
echo -e "${GREEN}✓${NC} Created $ENV_FILE"
echo ""

# Interactive credential input
echo "Please provide your credentials:"
echo "--------------------------------"
echo ""

# Anthropic API Key
read -p "Anthropic API Key: " ANTHROPIC_KEY
if [ -z "$ANTHROPIC_KEY" ]; then
    echo -e "${RED}Error: Anthropic API Key is required${NC}"
    exit 1
fi

# OpenAI API Key
read -p "OpenAI API Key: " OPENAI_KEY
if [ -z "$OPENAI_KEY" ]; then
    echo -e "${RED}Error: OpenAI API Key is required${NC}"
    exit 1
fi

# TogetherAI API Key (Optional)
read -p "TogetherAI API Key (optional, press Enter to skip): " TOGETHER_KEY

# AWS Credentials
read -p "AWS Access Key ID: " AWS_KEY
read -p "AWS Secret Access Key: " -s AWS_SECRET
echo ""
read -p "AWS Region [ap-south-1]: " AWS_REGION
AWS_REGION=${AWS_REGION:-ap-south-1}

# Database Connection
echo ""
echo "MySQL Database Configuration"

# Prompt for input
read -p "Database Host (e.g., localhost or 127.0.0.1): " DB_HOST
read -p "Database Port [3306]: " DB_PORT
DB_PORT=${DB_PORT:-3306}  # default to 3306 if empty
read -p "Database Name: " DB_NAME
read -p "Database User: " DB_USER
read -s -p "Database Password: " DB_PASSWORD
echo ""  # move to a new line after password input

# Build MySQL connection string
DB_CONN_STRING="Server=${DB_HOST};Port=${DB_PORT};Database=${DB_NAME};Uid=${DB_USER};Pwd=${DB_PASSWORD};"

# Write to .env file
cat > "$ENV_FILE" << EOF
# Anthropic API Configuration
ANTHROPIC_API_KEY=${ANTHROPIC_KEY}

# OpenAI API Configuration
OPENAI_API_KEY=${OPENAI_KEY}

# TogetherAI API Configuration (Optional)
TOGETHER_API_KEY=${TOGETHER_KEY}

# AWS Configuration
AWS_ACCESS_KEY_ID=${AWS_KEY}
AWS_SECRET_ACCESS_KEY=${AWS_SECRET}
AWS_REGION=${AWS_REGION}
AWS_DEFAULT_REGION=${AWS_REGION}

# Database Configuration (Azure SQL Server)
ConnectionStrings__DefaultConnection=${DB_CONN_STRING}
EOF

echo ""
echo -e "${GREEN}✓${NC} Configuration saved to $ENV_FILE"
echo ""

# Set proper permissions
chmod 600 "$ENV_FILE"
echo -e "${GREEN}✓${NC} Set secure permissions on .env file (600)"
echo ""

echo "=================================="
echo "Setup Complete!"
echo "=================================="
echo ""
echo "Next steps:"
echo "1. Review the configuration in $ENV_FILE"
echo "2. Start the application with: cd infra/dockerCompose && docker-compose up -d"
echo "3. Check logs with: docker-compose logs -f"
echo ""
echo -e "${YELLOW}Security Reminder:${NC}"
echo "- NEVER commit the .env file to version control"
echo "- Rotate credentials regularly"
echo "- Use different credentials for development and production"
echo ""
