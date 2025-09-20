import React, { useState, useCallback, useRef } from 'react';
import ReactDOM from 'react-dom';
import ReactFlow, {
  addEdge,
  applyEdgeChanges,
  applyNodeChanges,
  Background,
  Controls,
  MiniMap,
} from 'react-flow-renderer';

// Custom node types that will be implemented in subsequent tasks
const nodeTypes = {
  taskNode: ({ data }) => (
    <div className="task-node">
      <div className="node-header">
        <span className="node-icon">⚙️</span>
        <span className="node-title">{data.title || 'Task'}</span>
      </div>
      <div className="node-content">
        <p>{data.command || 'No command'}</p>
      </div>
    </div>
  ),
  conditionNode: ({ data }) => (
    <div className="condition-node">
      <div className="node-header">
        <span className="node-icon">🔀</span>
        <span className="node-title">{data.title || 'Condition'}</span>
      </div>
      <div className="node-content">
        <p>{data.expression || 'No expression'}</p>
      </div>
    </div>
  ),
  loopNode: ({ data }) => (
    <div className="loop-node">
      <div className="node-header">
        <span className="node-icon">🔄</span>
        <span className="node-title">{data.title || 'Loop'}</span>
      </div>
      <div className="node-content">
        <p>{data.loopType || 'No loop type'}</p>
      </div>
    </div>
  ),
  startNode: ({ data }) => (
    <div className="start-node">
      <div className="node-header">
        <span className="node-icon">▶️</span>
        <span className="node-title">Start</span>
      </div>
    </div>
  ),
  endNode: ({ data }) => (
    <div className="end-node">
      <div className="node-header">
        <span className="node-icon">⏹️</span>
        <span className="node-title">End</span>
      </div>
    </div>
  ),
};

// Main WorkflowCanvas component
const WorkflowCanvas = ({ initialNodes = [], initialEdges = [], onNodesChange, onEdgesChange, onConnect }) => {
  const [nodes, setNodes] = useState(initialNodes);
  const [edges, setEdges] = useState(initialEdges);
  const reactFlowWrapper = useRef(null);
  const [reactFlowInstance, setReactFlowInstance] = useState(null);

  const onNodesChangeHandler = useCallback(
    (changes) => {
      const newNodes = applyNodeChanges(changes, nodes);
      setNodes(newNodes);
      if (onNodesChange) onNodesChange(newNodes);
    },
    [nodes, onNodesChange]
  );

  const onEdgesChangeHandler = useCallback(
    (changes) => {
      const newEdges = applyEdgeChanges(changes, edges);
      setEdges(newEdges);
      if (onEdgesChange) onEdgesChange(newEdges);
    },
    [edges, onEdgesChange]
  );

  const onConnectHandler = useCallback(
    (connection) => {
      const newEdges = addEdge(connection, edges);
      setEdges(newEdges);
      if (onConnect) onConnect(newEdges);
    },
    [edges, onConnect]
  );

  const onDragOver = useCallback((event) => {
    event.preventDefault();
    event.dataTransfer.dropEffect = 'move';
  }, []);

  const onDrop = useCallback(
    (event) => {
      event.preventDefault();

      if (!reactFlowInstance) return;

      const reactFlowBounds = reactFlowWrapper.current.getBoundingClientRect();
      const type = event.dataTransfer.getData('application/reactflow');
      const position = reactFlowInstance.project({
        x: event.clientX - reactFlowBounds.left,
        y: event.clientY - reactFlowBounds.top,
      });

      const newNode = {
        id: `${type}_${Date.now()}`,
        type,
        position,
        data: { title: `New ${type}` },
      };

      const newNodes = [...nodes, newNode];
      setNodes(newNodes);
      if (onNodesChange) onNodesChange(newNodes);
    },
    [reactFlowInstance, nodes, onNodesChange]
  );

  return (
    <div className="workflow-canvas" ref={reactFlowWrapper} style={{ width: '100%', height: '600px' }}>
      <ReactFlow
        nodes={nodes}
        edges={edges}
        onNodesChange={onNodesChangeHandler}
        onEdgesChange={onEdgesChangeHandler}
        onConnect={onConnectHandler}
        onInit={setReactFlowInstance}
        onDrop={onDrop}
        onDragOver={onDragOver}
        nodeTypes={nodeTypes}
        fitView
      >
        <Background />
        <Controls />
        <MiniMap />
      </ReactFlow>
    </div>
  );
};

// Export functions to be called from Blazor
window.WorkflowBuilder = {
  init: (elementId, options = {}) => {
    const element = document.getElementById(elementId);
    if (!element) {
      console.error(`Element with id '${elementId}' not found`);
      return;
    }

    ReactDOM.render(
      <WorkflowCanvas
        initialNodes={options.initialNodes || []}
        initialEdges={options.initialEdges || []}
        onNodesChange={options.onNodesChange}
        onEdgesChange={options.onEdgesChange}
        onConnect={options.onConnect}
      />,
      element
    );
  },

  destroy: (elementId) => {
    const element = document.getElementById(elementId);
    if (element) {
      ReactDOM.unmountComponentAtNode(element);
    }
  },

  // Helper function to add a new node
  addNode: (elementId, nodeData) => {
    // This will be implemented in subsequent tasks
    console.log('addNode called with:', nodeData);
  },

  // Helper function to get current workflow data
  getWorkflowData: (elementId) => {
    // This will be implemented in subsequent tasks
    console.log('getWorkflowData called');
    return { nodes: [], edges: [] };
  },

  // Helper function to load workflow data
  loadWorkflowData: (elementId, workflowData) => {
    // This will be implemented in subsequent tasks
    console.log('loadWorkflowData called with:', workflowData);
  }
};

export default WorkflowCanvas;