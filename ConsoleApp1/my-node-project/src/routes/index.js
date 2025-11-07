const express = require('express');
const { getAllItems, getItemById } = require('../controllers');

const router = express.Router();

const setRoutes = (app) => {
    router.get('/items', getAllItems);
    router.get('/items/:id', getItemById);
    
    app.use('/api', router);
};

module.exports = setRoutes;