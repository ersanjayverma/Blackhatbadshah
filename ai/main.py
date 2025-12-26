import aiosqlite
from langgraph.checkpoint.sqlite.aio import AsyncSqliteSaver
from contextlib import asynccontextmanager
from typing import TypedDict, Annotated, List
from fastapi.middleware.cors import CORSMiddleware
from fastapi import FastAPI, Depends, HTTPException, status,Request
from fastapi.security import HTTPBearer, HTTPAuthorizationCredentials
from pydantic import BaseModel
from fastapi.responses import StreamingResponse
import httpx
import json
import os
import asyncio
import base64
from pathlib import Path
from jose import jwt
from langchain_community.utilities import StackExchangeAPIWrapper
from langchain_community.tools import StackExchangeTool
from langchain_anthropic import ChatAnthropic
from langchain_core.messages import BaseMessage, HumanMessage, AIMessage,AIMessageChunk
from langgraph.graph import StateGraph, START, END
from langgraph.graph.message import add_messages
from langgraph.prebuilt import ToolNode, tools_condition
from langchain_community.tools import DuckDuckGoSearchRun, WikipediaQueryRun
from langchain_community.utilities import WikipediaAPIWrapper

# =====================================================
# JWT / KEYCLOAK CONFIG  (matches ASP.NET config)
# =====================================================
AUTHORITY = "https://auth.blackhatbadshah.com/realms/blackhatbadshah"
AUDIENCE = "blackhatbadshah-api"
ALGORITHMS = ["RS256"]
JWKS_URL = f"{AUTHORITY}/protocol/openid-connect/certs"

security = HTTPBearer()
_jwks_cache = None

@asynccontextmanager
async def lifespan(api: FastAPI):
    async with aiosqlite.connect("bhb_test.db") as conn:
        saver = AsyncSqliteSaver(conn)
        api.state.graph = graph.compile(checkpointer=saver)
        yield

# =====================================================
# FASTAPI
# =====================================================
api = FastAPI(
    title="BlackhatBadshah LangGraph API",
    lifespan=lifespan,
)

async def verify_jwt(
    credentials: HTTPAuthorizationCredentials = Depends(security),
):
    global _jwks_cache
    token = credentials.credentials

    try:
        if not _jwks_cache:
            async with httpx.AsyncClient() as client:
                resp = await client.get(JWKS_URL)
                resp.raise_for_status()
                _jwks_cache = resp.json()

        payload = jwt.decode(
            token,
            _jwks_cache,
            algorithms=ALGORITHMS,
            audience=AUDIENCE,
            issuer=AUTHORITY,
        )

        return payload

    except Exception:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Invalid or expired token",
        )


# =====================================================
# LLM
# =====================================================
ANTHROPIC_API_KEY = os.environ.get("ANTHROPIC_API_KEY")

if not ANTHROPIC_API_KEY:
    raise RuntimeError("ANTHROPIC_API_KEY is not set in environment")

llm = ChatAnthropic(
    model="claude-sonnet-4-5-20250929",
    api_key=ANTHROPIC_API_KEY,
    streaming=True,
)

# =====================================================
# DOCUMENT PROCESSING CONSTANTS
# =====================================================
MAX_DOCUMENT_SIZE_BYTES = 2 * 1024 * 1024  # 2MB limit
SUPPORTED_TEXT_EXTENSIONS = {'.txt', '.md', '.py', '.js', '.json', '.xml', '.csv', '.log', '.yaml', '.yml'}

def validate_and_decode_document(
    document_base64: str,
    document_name: str | None
) -> tuple[str, str]:
    """
    Validates and decodes a base64-encoded text document.

    Args:
        document_base64: Base64-encoded document string
        document_name: Optional filename for validation

    Returns:
        Tuple of (decoded_text, filename)

    Raises:
        HTTPException: If validation fails
    """
    # Validate base64 format
    try:
        decoded_bytes = base64.b64decode(document_base64)
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=f"Invalid base64 encoding: {str(e)}"
        )

    # Check size limit (2MB)
    if len(decoded_bytes) > MAX_DOCUMENT_SIZE_BYTES:
        size_mb = len(decoded_bytes) / (1024 * 1024)
        raise HTTPException(
            status_code=status.HTTP_413_REQUEST_ENTITY_TOO_LARGE,
            detail=f"Document size ({size_mb:.2f}MB) exceeds 2MB limit"
        )

    # Validate file extension if name provided
    if document_name:
        extension = Path(document_name).suffix.lower()
        if extension not in SUPPORTED_TEXT_EXTENSIONS:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail=f"Unsupported file type: {extension}. Supported: {', '.join(SUPPORTED_TEXT_EXTENSIONS)}"
            )

    # Decode to text (UTF-8)
    try:
        decoded_text = decoded_bytes.decode('utf-8')
    except UnicodeDecodeError:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="Document is not valid UTF-8 text"
        )

    # Use provided name or default
    filename = document_name if document_name else "document.txt"

    return decoded_text, filename

# =====================================================
# LANGGRAPH STATE
# =====================================================
class Data(TypedDict):
    messages: Annotated[List[BaseMessage], add_messages]

# =====================================================
# TOOLS
# =====================================================
tool_search = DuckDuckGoSearchRun(region="in-en")
stack_api = StackExchangeAPIWrapper()
tool_stack = StackExchangeTool(api_wrapper=stack_api)
wiki_api = WikipediaAPIWrapper(
    top_k_results=3,
    doc_content_chars_max=5000
)
tool_wiki = WikipediaQueryRun(api_wrapper=wiki_api)

tools = [tool_search, tool_wiki,tool_stack]
llmt = llm.bind_tools(tools)

# =====================================================
# GRAPH NODES
# =====================================================
async def chat_node(state: Data):
    response = await llmt.ainvoke(state["messages"])
    return {"messages": [response]}

tool_node = ToolNode(tools)

# =====================================================
# GRAPH SETUP
# =====================================================
graph = StateGraph(Data)
graph.add_node("chat_node", chat_node)
graph.add_node("tools", tool_node)

graph.add_edge(START, "chat_node")
graph.add_conditional_edges("chat_node", tools_condition)
graph.add_edge("tools", "chat_node")
graph.add_edge("chat_node", END)




class ChatRequest(BaseModel):
    thread_id: str
    message: str
    document_base64: str | None = None
    document_name: str | None = None

class ChatResponse(BaseModel):
    reply: str

api.add_middleware(
    CORSMiddleware,
    allow_origins=[
        "https://blackhatbadshah.com",
        "https://www.blackhatbadshah.com",
        "https://api.blackhatbadshah.com",
        "https://www.api.blackhatbadshah.com"
    ],
    allow_credentials=True,
    allow_methods=["GET", "POST", "OPTIONS"],
    allow_headers=[
        "Authorization",
        "Content-Type",
    ],
    expose_headers=["Content-Type", "X-Accel-Buffering"]
)

@api.post("/chat", response_model=ChatResponse)
async def chat(
    req: ChatRequest,
    request: Request,
    token=Depends(verify_jwt),
):
    try:
        # Build message content
        message_content = req.message

        # If document provided, validate and include it
        if req.document_base64:
            decoded_text, filename = validate_and_decode_document(
                req.document_base64,
                req.document_name
            )

            # Construct multimodal message with document context
            message_content = [
                {
                    "type": "text",
                    "text": f"Document ({filename}):\n\n{decoded_text}\n\n---\n\n"
                },
                {
                    "type": "text",
                    "text": req.message
                }
            ]

        # Invoke graph with message (string or list)
        result = await request.app.state.graph.ainvoke(
            {"messages": [HumanMessage(content=message_content)]},
            config={"configurable": {"thread_id": req.thread_id}},
        )
    except HTTPException:
        # Re-raise validation errors (with proper status codes)
        raise
    except Exception as e:
        # Never crash the API
        return ChatResponse(reply=f"Analyzer failed: {str(e)}")

    messages = result.get("messages", [])
    if not messages:
        return ChatResponse(reply="Analyzer returned no messages")

    last = messages[-1]

    if isinstance(last, AIMessage):
        reply = last.content

        # Claude now returns list segments
        if isinstance(reply, list):
            reply = "".join(
                part.get("text", "") if isinstance(part, dict) else str(part)
                for part in reply
            )

        # Safety: ensure string
        reply = str(reply)

        return ChatResponse(reply=reply)
    # Fallback (should rarely happen)
    return ChatResponse(reply=str(last))



@api.post("/chat/stream")
async def chat_stream(
    req: ChatRequest,
    request: Request, 
    token=Depends(verify_jwt),
):
    async def event_generator():
        try:
            yield ": connected\n\n"

            # Use astream with "messages" mode
            async for chunk in request.app.state.graph.astream(
                {"messages": [HumanMessage(content=req.message)]},
                config={"configurable": {"thread_id": req.thread_id}},
                stream_mode="messages",
            ):
                # 1. Handle Tuple Unpacking: chunk is often (AIMessageChunk, metadata)
                msg = chunk[0] if isinstance(chunk, tuple) else chunk
                
                # 2. Extract content from AIMessageChunk or dict
                content = ""
                if isinstance(msg, AIMessageChunk):
                    if isinstance(msg.content, list):
                        content = "".join(
                            part.get("text", "")
                            for part in msg.content
                        )
                    else:
                        content = str(msg.content)
                elif isinstance(msg, dict) and "content" in msg:
                    content = msg["content"]
                elif hasattr(msg, "content"):
                    content = msg.content

                # 3. Yield only if there is actual text
                if content:
                    yield f"data: {json.dumps({'token': content})}\n\n"

            yield "data: [DONE]\n\n"

        except Exception as e:
            # Detailed logging to help you see the exact failure in the console
            import traceback
            error_detail = traceback.format_exc()
            print(f"CRITICAL STREAM ERROR:\n{error_detail}")
            
            # Send a cleaner error to the client
            yield f"data: {json.dumps({'error': str(e)})}\n\n"

    return StreamingResponse(
        event_generator(),
        media_type="text/event-stream",
        headers={
            "Content-Type": "text/event-stream",
            "Cache-Control": "no-cache",
            "Connection": "keep-alive",
            "X-Accel-Buffering": "no",  # CRITICAL: Disables Nginx buffering
        },
    )


# =====================================================
# HEALTH
# =====================================================
@api.get("/health")
async def health():
    return {"status": "ok"}
