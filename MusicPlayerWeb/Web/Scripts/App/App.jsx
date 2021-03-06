﻿import React from 'react';
import { connect } from 'react-redux';
import Router from 'react-router';
import { HashRouter, Route, Link } from 'react-router-dom';
import '../../Style/App.scss';
import { csharpDispatcher } from './CSharpDispatcher.jsx';

import Home from './PageComponents/Home.jsx';
import PlayList from './PageComponents/PlayList.jsx';
import Server from './PageComponents/Server.jsx';
import Client from './PageComponents/Client.jsx';
import Copy from './PageComponents/Copy.jsx';
import Video from './PageComponents/Video.jsx';
import Radio from './PageComponents/Radio.jsx';
import EditRadio from './PageComponents/EditRadio.jsx';


/**
 * @class The React app.
 */
class App extends React.Component {
    constructor(props) {
        super(props);
        window.CSSharpDispatcher = csharpDispatcher;
        this.router = null;
    }

    /**
     * @description Prevent unescesary render cycles.
     * @param {object} nextprops 
     * @param {object} nextstate 
     */
    shouldComponentUpdate(nextprops, nextstate) {
        if (nextprops.currentSong != this.props.currentSong 
            || JSON.stringify(nextprops.serverInfo) != JSON.stringify(this.props.serverInfo) 
            || this.props.copyProgress != nextprops.copyProgress) {
            return true;
        }

        return false;
    }

    componentWillReceiveProps(nextprops) {
        if (nextprops.serverInfo && !nextprops.serverInfo.IsHost && nextprops.serverInfo.VideoUrl) {
            this.refs.router.history.push("/video");
        }
    }

    /**
     * @description Render navigation and router.
     */
    render() {
        return (
            <HashRouter ref="router">
                <div className="h-100">
                    <div className="navbar p-0">
                        <ul className="navigation">
                            <li><Link to="/"><i className="fa fa-home" /></Link></li>
                            {this.props.currentSong && <li><Link to="/playlist"><i className="fa fa-music" /></Link></li>}
                            {(this.props.currentSong && this.props.currentSong.IsInternetRadio) && <li><Link to="/radio"><i className="fab fa-soundcloud" /></Link></li>}
                            {this.props.serverInfo && this.props.serverInfo.IsHost && <li title={"Currently connected clients: " + this.props.serverInfo.Count}>
                                <Link to="/server"><i className="fas fa-broadcast-tower" /></Link>
                            </li>}
                            {this.props.serverInfo && !this.props.serverInfo.IsHost && <li title={"Currently connected to: " + this.props.serverInfo.Host}>
                                <Link to="/client"><i className="fas fa-signal" /></Link>
                            </li>}
                            {this.props.serverInfo && this.props.serverInfo.VideoUrl && <li title={"Currently playing: " + this.props.serverInfo.VideoUrl}>
                                <Link to="/video"><i className="fab fa-youtube" /></Link>
                            </li>}
                            {this.props.copyProgress != null && this.props.copyProgress != undefined && <li title={"Currently copying files: " + parseInt(this.props.copyProgress) + "%"}>
                                <Link to="/copy"><i className="far fa-copy" /></Link>
                            </li>}
                        </ul>
                    </div>
                    <div className="container-fluid mainContainer">
                        <Route path="/" component={Home}></Route>
                        <Route path="/playlist" component={PlayList}></Route>
                        <Route path="/server" component={Server}></Route>
                        <Route path="/client" component={Client}></Route>
                        <Route path="/copy" component={Copy}></Route>
                        <Route path="/video" component={Video}></Route>
                        <Route path="/radio" component={Radio}></Route>
                        <Route path="/radio/:id" component={EditRadio}></Route>
                    </div>
                </div>
            </HashRouter>
        );
    }
}

function mapStateToProps(state) {
  return { 
      currentSong: state.currentSong,
      serverInfo: state.serverInfo,
      copyProgress: state.copyProgress
    };
}

export default connect(mapStateToProps)(App);

